using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using System.Collections.Generic;
using WorldSystem.Data;
using System.Linq;
using WorldSystem.Generation;
using WorldSystem.Mesh;
namespace WorldSystem.Base
{
    public class ChunkManager : MonoBehaviour
    {
        private IChunkGenerator _chunkGenerator;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private Material chunkMaterial;

        [SerializeField] private float loadDistance = 100f;
        [SerializeField] private float chunkLoadBuffer = 1.2f;
        [SerializeField] private float bufferTimeSeconds = 30f;
        
        private HashSet<int2> _visibleChunkPositions = new();
        private PriorityQueue<int2> _chunkLoadQueue = new();
        private Vector3 _lastCameraPosition;
        private float _lastCameraHeight;
        private float _lastOrthoSize;
        private const float UPDATE_THRESHOLD = 16f;

        [SerializeField] private int poolSize = 512;
        public ChunkPool ChunkPool { get; private set; }

        [SerializeField] private int maxChunks = 512;

        private IChunkMeshBuilder _meshBuilder;

        [SerializeField] private int viewMaxYLevel = 20;
        public int ViewMaxYLevel
        {
            get => viewMaxYLevel;
            set
            {
                if (viewMaxYLevel != value)
                {
                    viewMaxYLevel = value;
                    HandleYLevelChange();
                }
            }
        }

        private int _lastViewMaxYLevel;  // Track Y-level changes

        private Dictionary<int2, HashSet<int>> _generatedYLevels = new();

        private Dictionary<int2, NativeArray<byte>> _chunkBlockData = new();
        private HashSet<int2> _generatedChunks = new();

        [SerializeField] private int maxCachedChunks = 2048;

        private PriorityQueue<int2> _chunkDistanceQueue = new();

        private ChunkPool _chunkPool;

        [SerializeField] private WorldGenerationSettings worldSettings;

        void Awake()
        {
            if (worldSettings == null)
            {
                Debug.LogError("World Generation Settings not assigned!");
                return;
            }

            _chunkGenerator = new ChunkGenerator(worldSettings);
            _meshBuilder = new ChunkMeshBuilder();
            _chunkGenerator.OnChunkGenerated += OnChunkGenerated;

            _chunkPool = new ChunkPool(chunkMaterial, transform, poolSize, maxChunks, bufferTimeSeconds);
            _lastViewMaxYLevel = viewMaxYLevel;  // Initialize last known Y-level
            UpdateVisibleChunks();

            // Initialize last known positions
            _lastCameraPosition = mainCamera.transform.position;
            _lastCameraHeight = _lastCameraPosition.y;
            _lastOrthoSize = mainCamera.orthographicSize;
        }

        void Update()
        {
            Vector3 currentCamPos = mainCamera.transform.position;
            float currentHeight = currentCamPos.y;
            float currentOrthoSize = mainCamera.orthographicSize;
            
            // Check if Y-level has changed
            if (_lastViewMaxYLevel != viewMaxYLevel)
            {
                HandleYLevelChange();
                _lastViewMaxYLevel = viewMaxYLevel;
            }
            // Regular position update check
            else if (Vector3.Distance(_lastCameraPosition, currentCamPos) > UPDATE_THRESHOLD ||
                Mathf.Abs(_lastCameraHeight - currentHeight) > UPDATE_THRESHOLD ||
                Mathf.Abs(_lastOrthoSize - currentOrthoSize) > 0.01f)
            {
                UpdateVisibleChunks();
                QueueMissingChunks();
                CleanupDistantChunks();
                
                _lastCameraPosition = currentCamPos;
                _lastCameraHeight = currentHeight;
                _lastOrthoSize = currentOrthoSize;
            }

            ProcessChunkQueue();
            _meshBuilder.Update();
        }

        void UpdateVisibleChunks()
        {
            _visibleChunkPositions.Clear();
            Vector3 camPos = mainCamera.transform.position;
            
            if (mainCamera.orthographic)
            {
                float aspect = mainCamera.aspect;
                float orthoWidth = mainCamera.orthographicSize * 2f * aspect;
                float orthoHeight = mainCamera.orthographicSize * 2f;
                
                orthoWidth *= chunkLoadBuffer;
                orthoHeight *= chunkLoadBuffer;
                
                int chunkDistanceX = Mathf.CeilToInt((orthoWidth * 0.5f) / ChunkData.SIZE);
                int chunkDistanceZ = Mathf.CeilToInt((orthoHeight * 0.5f) / ChunkData.SIZE);

                int2 centerChunk = new int2(
                    Mathf.FloorToInt(camPos.x / ChunkData.SIZE),
                    Mathf.FloorToInt(camPos.z / ChunkData.SIZE)
                );

                for (int x = -chunkDistanceX; x <= chunkDistanceX; x++)
                for (int z = -chunkDistanceZ; z <= chunkDistanceZ; z++)
                {
                    int2 chunkPos = new int2(centerChunk.x + x, centerChunk.y + z);
                    _visibleChunkPositions.Add(chunkPos);
                }
            }
            else
            {
                float viewDistance = loadDistance * chunkLoadBuffer;
                int2 centerChunk = new int2(
                    Mathf.FloorToInt(camPos.x / ChunkData.SIZE),
                    Mathf.FloorToInt(camPos.z / ChunkData.SIZE)
                );
                
                int chunkDistance = Mathf.CeilToInt(viewDistance / ChunkData.SIZE);
                for (int x = -chunkDistance; x <= chunkDistance; x++)
                for (int z = -chunkDistance; z <= chunkDistance; z++)
                {
                    int2 chunkPos = new int2(centerChunk.x + x, centerChunk.y + z);
                    _visibleChunkPositions.Add(chunkPos);
                }
            }
        }

        void QueueMissingChunks()
        {
            foreach (var pos in _visibleChunkPositions)
            {
                // First check if we already have this chunk in memory
                if (_generatedChunks.Contains(pos))
                {
                    // Even if chunk is generated, we need to ensure it's visible at current Y-level
                    if (!_chunkPool.HasActiveChunkAtPosition(pos, viewMaxYLevel))
                    {
                        if (_chunkBlockData.TryGetValue(pos, out var blocks))
                        {
                            CreateChunkObject(pos, blocks, default);
                        }
                    }
                }
                // If not generated, queue it for generation
                else if (!_chunkGenerator.IsGenerating(pos) && !_chunkLoadQueue.Contains(pos))
                {
                    float priority = Vector2.Distance(
                        new Vector2(mainCamera.transform.position.x, mainCamera.transform.position.z),
                        new Vector2(pos.x * ChunkData.SIZE, pos.y * ChunkData.SIZE)
                    );
                    _chunkLoadQueue.Enqueue(pos, priority);
                }
            }
        }

        private void OnChunkGenerated(ChunkData chunkData)
        {
            int2 position2D = new int2(chunkData.position.x, chunkData.position.z);
            
            if (!_generatedChunks.Contains(position2D))
            {
                var permanentBlocks = new NativeArray<byte>(chunkData.blocks.Length, 
                    Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                permanentBlocks.CopyFrom(chunkData.blocks);
                
                _chunkBlockData[position2D] = permanentBlocks;
                _generatedChunks.Add(position2D);
            }

            if (!_generatedYLevels.ContainsKey(position2D))
            {
                _generatedYLevels[position2D] = new HashSet<int>();
            }
            _generatedYLevels[position2D].Add(viewMaxYLevel);

            CreateChunkObject(position2D, chunkData.blocks, default);
        }

        void ProcessChunkQueue()
        {
            _meshBuilder.Update();

            // Check for completed jobs from the chunk generator
            _chunkGenerator.Update();

            // Process new chunks from the queue
            while (_chunkLoadQueue.Count > 0 && !_chunkGenerator.IsGenerating(_chunkLoadQueue.Peek()))
            {
                var pos = _chunkLoadQueue.Dequeue();
                _chunkGenerator.QueueChunkGeneration(pos);
            }
        }

        void CreateChunkObject(int2 position, NativeArray<byte> blocks, JobHandle dependency)
        {
            var chunkResult = _chunkPool.GetChunk(position, viewMaxYLevel);
            if (chunkResult == null) return;

            var (chunk, meshFilter, shadowMeshFilter) = chunkResult.Value;
            _meshBuilder.QueueMeshBuild(position, blocks, meshFilter, shadowMeshFilter, viewMaxYLevel);
        }

        void CleanupDistantChunks()
        {
            var activeChunkPositions = _chunkPool.GetActiveChunkPositions().ToList();
            
            foreach (var (pos, yLevel) in activeChunkPositions)
            {
                if (!_visibleChunkPositions.Contains(pos))
                {
                    _chunkPool.DeactivateChunk(pos, yLevel);
                }
            }

            _chunkPool.CheckBufferTimeout();

            var newQueue = new PriorityQueue<int2>();
            while (_chunkLoadQueue.Count > 0)
            {
                var pos = _chunkLoadQueue.Dequeue();
                if (_visibleChunkPositions.Contains(pos))
                {
                    newQueue.Enqueue(pos, Vector2.Distance(
                        new Vector2(mainCamera.transform.position.x, mainCamera.transform.position.z),
                        new Vector2(pos.x * ChunkData.SIZE, pos.y * ChunkData.SIZE)
                    ));
                }
            }
            _chunkLoadQueue = newQueue;

            CleanupExcessChunks();
        }

        void OnDestroy()
        {
            _chunkGenerator.Dispose();
            _meshBuilder.Dispose();
            _chunkPool.Cleanup();

            // Add cleanup for stored block data
            foreach (var blocks in _chunkBlockData.Values)
            {
                if (blocks.IsCreated)
                    blocks.Dispose();
            }
            _chunkBlockData.Clear();
        }

        private void UpdateShaderOrthoSize()
        {
            Camera cam = GetComponent<Camera>();
            Shader.SetGlobalFloat("_OrthoSize", cam.orthographicSize);
        }

        private void HandleYLevelChange()
        {
            // First, deactivate all chunks at the old Y-level
            var activeChunks = _chunkPool.GetActiveChunkPositions().ToList();
            foreach (var (pos, oldYLevel) in activeChunks)
            {
                if (oldYLevel != viewMaxYLevel)
                {
                    _chunkPool.DeactivateChunk(pos, oldYLevel);
                }
            }

            // Then handle chunks at the new Y-level
            foreach (var pos in _visibleChunkPositions)
            {
                if (!_generatedChunks.Contains(pos))
                {
                    if (!_chunkGenerator.IsGenerating(pos) && !_chunkLoadQueue.Contains(pos))
                    {
                        float priority = Vector2.Distance(
                            new Vector2(mainCamera.transform.position.x, mainCamera.transform.position.z),
                            new Vector2(pos.x * ChunkData.SIZE, pos.y * ChunkData.SIZE)
                        );
                        _chunkLoadQueue.Enqueue(pos, priority);
                    }
                }
                else if (_chunkBlockData.TryGetValue(pos, out var blocks))
                {
                    if (!_chunkPool.HasActiveChunkAtPosition(pos, viewMaxYLevel))
                    {
                        CreateChunkObject(pos, blocks, default);
                    }
                }
            }

            // Force an immediate cleanup check
            _chunkPool.CheckBufferTimeout();
        }

        private void CleanupExcessChunks()
        {
            if (_chunkBlockData.Count <= maxCachedChunks) return;

            Vector3 camPos = mainCamera.transform.position;
            int2 cameraPosInChunks = new int2(
                Mathf.FloorToInt(camPos.x / ChunkData.SIZE),
                Mathf.FloorToInt(camPos.z / ChunkData.SIZE)
            );

            _chunkDistanceQueue.Clear();
            foreach (var chunkPos in _chunkBlockData.Keys)
            {
                if (_visibleChunkPositions.Contains(chunkPos)) continue;

                float distance = Vector2.Distance(
                    new Vector2(cameraPosInChunks.x, cameraPosInChunks.y),
                    new Vector2(chunkPos.x, chunkPos.y)
                );
                _chunkDistanceQueue.Enqueue(chunkPos, -distance);
            }

            int chunksToRemove = _chunkBlockData.Count - maxCachedChunks;
            for (int i = 0; i < chunksToRemove && _chunkDistanceQueue.Count > 0; i++)
            {
                var chunkPos = _chunkDistanceQueue.Dequeue();
                
                if (_chunkBlockData.TryGetValue(chunkPos, out var blocks))
                {
                    if (blocks.IsCreated)
                        blocks.Dispose();
                    _chunkBlockData.Remove(chunkPos);
                }

                _generatedChunks.Remove(chunkPos);
                _generatedYLevels.Remove(chunkPos);
            }
        }

        void OnGUI()
        {
            GUI.Label(new Rect(10, 10, 200, 20), 
                $"Cached Chunks: {_chunkBlockData.Count}/{maxCachedChunks}");
        }

        private void Start()
        {
            chunkMaterial.SetFloat("_WorldSeed", _chunkGenerator.seed);
            chunkMaterial.SetFloat("_ColorVariationStrength", 0.05f);
            chunkMaterial.SetFloat("_ColorVariationScale", 25f);
        }

        public void ResetWorld()
        {
            // Complete any pending operations
            _chunkGenerator?.Dispose();
            _meshBuilder?.Dispose();

            // Clear all chunk data
            foreach (var chunk in _chunkBlockData)
            {
                if (chunk.Value.IsCreated)
                    chunk.Value.Dispose();
            }
            _chunkBlockData.Clear();
            _generatedChunks.Clear();
            _generatedYLevels.Clear();
            _visibleChunkPositions.Clear();
            _chunkDistanceQueue.Clear();
            _chunkLoadQueue.Clear();

            // Reset the chunk pool
            _chunkPool?.Cleanup();

            // Create new instances
            _chunkGenerator = new ChunkGenerator(worldSettings);
            _meshBuilder = new ChunkMeshBuilder();
            _chunkPool = new ChunkPool(chunkMaterial, transform, poolSize, maxChunks, bufferTimeSeconds);

            // Reattach the event handler
            _chunkGenerator.OnChunkGenerated += OnChunkGenerated;

            // Force an immediate update of visible chunks and queue generation
            UpdateVisibleChunks();
            QueueMissingChunks(); // Add this to start generating new chunks
            
            // Update last known camera position to force immediate update
            _lastCameraPosition = mainCamera.transform.position + new Vector3(UPDATE_THRESHOLD * 2, 0, 0);
            _lastCameraHeight = _lastCameraPosition.y;
            _lastOrthoSize = mainCamera.orthographicSize;
        }

        // Add this to allow external access to world settings
        public void UpdateWorldSettings(WorldGenerationSettings newSettings)
        {
            if (newSettings == null)
            {
                Debug.LogError("Attempted to update world settings with null value!");
                return;
            }

            worldSettings = newSettings;
            ResetWorld();
        }
    }
} 