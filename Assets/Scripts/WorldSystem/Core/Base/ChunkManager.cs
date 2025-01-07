using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
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

        [SerializeField] private float loadDistance = 100f;  // Fallback for perspective camera if orthographic is false for whatever reason
        [SerializeField] private float chunkLoadBuffer = 1.2f; // 1.0 = exact fit, 1.2 = 20% extra, etc.
        
        private HashSet<int2> _visibleChunkPositions = new();
        private PriorityQueue<int2> _chunkLoadQueue = new();
        private Vector3 _lastCameraPosition;
        private float _lastCameraHeight;
        private const float UPDATE_THRESHOLD = 16f; // Only update when camera moves 1 chunk

        [SerializeField] private int poolSize = 512; // Adjust based on your max visible chunks
        private ChunkPool _chunkPool;

        [SerializeField] private int maxChunks = 512; // Maximum total chunks that can exist

        private IChunkMeshBuilder _meshBuilder;

        private float _lastOrthoSize; // Add this field for tracking orthographic size changes.
        [SerializeField] private float bufferTimeSeconds = 5f; // Add this field for buffer time configuration

        [Header("Atmosphere Settings")]
        [SerializeField] private Color atmosphereColor = new Color(0.6f, 0.8f, 1.0f, 1.0f);
        [SerializeField] private float atmosphereDensity = 0.3f;
        [SerializeField] private float atmosphereHeight = 50f;
        [SerializeField] private float distanceFactor = 100f;

        void Awake()
        {
            _chunkGenerator = new ChunkGenerator();
            _meshBuilder = new ChunkMeshBuilder();
            _chunkGenerator.OnChunkGenerated += OnChunkGenerated;

            _chunkPool = new ChunkPool(chunkMaterial, transform, poolSize, maxChunks, bufferTimeSeconds);
            UpdateVisibleChunks();

            // Set the atmosphere properties on the material
            chunkMaterial.SetColor("_AtmosphereColor", atmosphereColor);
            chunkMaterial.SetFloat("_AtmosphereDensity", atmosphereDensity);
            chunkMaterial.SetFloat("_AtmosphereHeight", atmosphereHeight);
            chunkMaterial.SetFloat("_DistanceFactor", distanceFactor);
        }

        void Update()
        {
            Vector3 currentCamPos = mainCamera.transform.position;
            float currentHeight = currentCamPos.y;
            float currentOrthoSize = mainCamera.orthographicSize;
            
            // Only update chunks if camera moved enough or ortho size changed
            if (Vector3.Distance(_lastCameraPosition, currentCamPos) > UPDATE_THRESHOLD ||
                Mathf.Abs(_lastCameraHeight - currentHeight) > UPDATE_THRESHOLD ||
                Mathf.Abs(_lastOrthoSize - currentOrthoSize) > 0.01f)  // Small threshold for ortho changes
            {
                UpdateVisibleChunks();
                QueueMissingChunks();
                CleanupDistantChunks();
                
                _lastCameraPosition = currentCamPos;
                _lastCameraHeight = currentHeight;
                _lastOrthoSize = currentOrthoSize;
            }

            // This still needs to run every frame to process the queue
            ProcessChunkQueue();
            _meshBuilder.Update();

            // Only what matters for orthographic
            chunkMaterial.SetFloat("_OrthoSize", mainCamera.orthographicSize);
            chunkMaterial.SetColor("_AtmosphereColor", atmosphereColor);
            chunkMaterial.SetFloat("_AtmosphereDensity", atmosphereDensity);
            chunkMaterial.SetFloat("_AtmosphereHeight", atmosphereHeight);
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
                
                // Apply buffer to the view distances
                orthoWidth *= chunkLoadBuffer;
                orthoHeight *= chunkLoadBuffer;
                
                // Calculate chunk distances for width and height separately
                int chunkDistanceX = Mathf.CeilToInt((orthoWidth * 0.5f) / ChunkData.SIZE);
                int chunkDistanceZ = Mathf.CeilToInt((orthoHeight * 0.5f) / ChunkData.SIZE);

                // Convert camera position to chunk coordinates
                int2 centerChunk = new int2(
                    Mathf.FloorToInt(camPos.x / ChunkData.SIZE),
                    Mathf.FloorToInt(camPos.z / ChunkData.SIZE)
                );

                // Use different ranges for X and Z to create a rectangular area
                for (int x = -chunkDistanceX; x <= chunkDistanceX; x++)
                for (int z = -chunkDistanceZ; z <= chunkDistanceZ; z++)
                {
                    int2 chunkPos = new int2(centerChunk.x + x, centerChunk.y + z);
                    _visibleChunkPositions.Add(chunkPos);
                }
            }
            else
            {
                // Fallback for perspective camera (using the buffer as a direct multiplier)
                float viewDistance = Mathf.Min(loadDistance, camPos.y * 2f) * chunkLoadBuffer;
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
            Vector3 camPos = mainCamera.transform.position;
            foreach (var chunkPos in _visibleChunkPositions)
            {
                if (!_chunkPool.HasActiveChunkAtPosition(chunkPos) && 
                    !_chunkGenerator.IsGenerating(chunkPos) && 
                    !_chunkLoadQueue.Contains(chunkPos))
                {
                    float priority = Vector2.Distance(
                        new Vector2(camPos.x, camPos.z),
                        new Vector2(chunkPos.x * ChunkData.SIZE, chunkPos.y * ChunkData.SIZE)
                    );
                    _chunkLoadQueue.Enqueue(chunkPos, priority);
                }
            }
        }

        private void OnChunkGenerated(ChunkData chunkData)
        {
            CreateChunkObject(chunkData.position, chunkData.blocks, chunkData.heightMap);
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

        void CreateChunkObject(int2 position, NativeArray<byte> blocks, NativeArray<HeightPoint> heightMap)
        {
            var chunkResult = _chunkPool.GetChunk(position);
            if (chunkResult == null)
            {
                Debug.LogWarning($"Cannot create chunk at {position}: At maximum chunk limit ({maxChunks}). Consider increasing maxChunks if needed.");
                return;
            }

            var (chunk, meshFilter, shadowMeshFilter) = chunkResult.Value;
            
            // Create a copy of heightMap for the mesh job
            var heightMapCopy = new NativeArray<HeightPoint>(heightMap.Length, Allocator.Persistent);
            heightMapCopy.CopyFrom(heightMap);
            
            _meshBuilder.QueueMeshBuild(position, heightMapCopy, meshFilter, shadowMeshFilter);
        }

        void CleanupDistantChunks()
        {
            // Get all active chunk positions from the pool
            var activeChunkPositions = _chunkPool.GetActiveChunkPositions();
            
            // Find chunks that are no longer visible
            var chunksToRemove = activeChunkPositions
                .Where(pos => !_visibleChunkPositions.Contains(pos))
                .ToList();

            // Move them to inactive buffer
            foreach (var pos in chunksToRemove)
            {
                _chunkPool.DeactivateChunk(pos);
            }

            _chunkPool.CheckBufferTimeout();

            // Clean queue
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
        }

        void OnDestroy()
        {
            _chunkGenerator.Dispose();
            _meshBuilder.Dispose();
            _chunkPool.Cleanup();
        }
    }
} 