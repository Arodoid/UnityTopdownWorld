using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using System.Collections.Generic;
using WorldSystem.Data;
using System.Linq;
using WorldSystem.Mesh;
using WorldSystem.Generation;
// using WorldSystem.Generation.Features;
using Unity.Burst;
using System.Threading.Tasks;
using WorldSystem.Persistence;

namespace WorldSystem.Base
{
    public class ChunkManager : MonoBehaviour
    {
        public event System.Action<int2> OnChunkLoaded;
        public event System.Action<int2> OnChunkUnloaded;

        private WorldGenSettings _worldSettings;
        private WorldGenerator _worldGenerator;
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
        private Quaternion _lastCameraRotation;
        private const float UPDATE_THRESHOLD = 16f;

        [SerializeField] private int poolSize = 512;
        public ChunkPool ChunkPool { get; private set; }

        [SerializeField] private int maxChunks = 512;

        private ChunkMeshManager _meshManager;

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

        private int _lastViewMaxYLevel;

        private Dictionary<int2, HashSet<int>> _generatedYLevels = new();
        private Dictionary<int2, NativeArray<byte>> _chunkBlockData = new();
        private HashSet<int2> _generatedChunks = new();

        [SerializeField] private int maxCachedChunks = 2048;
        private PriorityQueue<int2> _chunkDistanceQueue = new();
        private ChunkPool _chunkPool;


        // Add this field to track pending jobs
        private Dictionary<int2, (JobHandle handle, NativeArray<byte> blocks, 
            NativeArray<Core.HeightPoint> heightMap)> _pendingJobs = new();

        // Add field to track heightmap arrays
        private Dictionary<int2, NativeArray<Data.HeightPoint>> _chunkHeightMaps = new();

        // Create a job for heightmap conversion
        [BurstCompile]
        private struct HeightMapConversionJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<Core.HeightPoint> sourceHeightMap;
            public NativeArray<Data.HeightPoint> targetHeightMap;

            public void Execute(int i)
            {
                targetHeightMap[i] = new Data.HeightPoint
                {
                    height = sourceHeightMap[i].height,
                    blockType = sourceHeightMap[i].blockType
                };
            }
        }

        private ChunkPersistenceManager _persistenceManager;
        [SerializeField] private string worldName = "DefaultWorld";

        private HashSet<int2> _dirtyChunks = new HashSet<int2>();

        private struct ChunkBounds
        {
            public Vector3 center;
            public Vector3 extents;
            
            public static readonly Vector3 DefaultExtents = new(
                ChunkData.SIZE * 0.5f,
                ChunkData.HEIGHT * 0.5f,
                ChunkData.SIZE * 0.5f
            );
        }

        private readonly Dictionary<int2, ChunkBounds> _chunkBoundsCache = new();
        private Plane[] _cachedFrustumPlanes = new Plane[6];
        private Vector3 _lastFrustumUpdatePos;
        private Quaternion _lastFrustumUpdateRot;

        // Add this field to track conversion jobs
        private Dictionary<int2, (JobHandle handle, NativeArray<Data.HeightPoint> heightMap, NativeArray<byte> blocks)> _pendingConversionJobs = new();

        public void Initialize(WorldGenSettings settings)
        {
        _worldSettings = settings;
        _worldGenerator = new WorldGenerator(_worldSettings);
        _meshManager = new ChunkMeshManager(BlockColors.Definitions);  // Pass BlockDefinitions directly

        _chunkPool = new ChunkPool(chunkMaterial, transform, poolSize, maxChunks, bufferTimeSeconds);
        _lastViewMaxYLevel = viewMaxYLevel;
        UpdateVisibleChunks();

        _lastCameraPosition = mainCamera.transform.position;
        _lastCameraHeight = _lastCameraPosition.y;
        _lastOrthoSize = mainCamera.orthographicSize;
        _lastCameraRotation = mainCamera.transform.rotation;

        _persistenceManager = new ChunkPersistenceManager(worldName);        }

        void Update()
        {
            Vector3 currentCamPos = mainCamera.transform.position;
            float currentHeight = currentCamPos.y;
            float currentOrthoSize = mainCamera.orthographicSize;
            Quaternion currentRotation = mainCamera.transform.rotation;
            
            // Check if Y-level has changed
            if (_lastViewMaxYLevel != viewMaxYLevel)
            {
                HandleYLevelChange();
                _lastViewMaxYLevel = viewMaxYLevel;
            }
            // Add rotation check to trigger updates
            else if (Vector3.Distance(_lastCameraPosition, currentCamPos) > UPDATE_THRESHOLD ||
                Mathf.Abs(_lastCameraHeight - currentHeight) > UPDATE_THRESHOLD ||
                Mathf.Abs(_lastOrthoSize - currentOrthoSize) > 0.01f ||
                Quaternion.Angle(_lastCameraRotation, currentRotation) > 1f) // Check rotation changes
            {
                UpdateVisibleChunks();
                QueueMissingChunks();
                CleanupDistantChunks();
                
                _lastCameraPosition = currentCamPos;
                _lastCameraHeight = currentHeight;
                _lastOrthoSize = currentOrthoSize;
                _lastCameraRotation = currentRotation; // Store last rotation
            }

            ProcessChunkQueue();
            _meshManager.Update();
            
            // Process any dirty chunks
            foreach (var dirtyChunk in _dirtyChunks)
            {
                if (_chunkBlockData.TryGetValue(dirtyChunk, out var blocks))
                {
                    var chunk = _chunkPool.GetChunk(dirtyChunk, viewMaxYLevel);
                    if (chunk.HasValue)
                    {
                        _meshManager.QueueMeshBuild(dirtyChunk, blocks, 
                            chunk.Value.meshFilter, chunk.Value.shadowFilter, viewMaxYLevel);
                    }
                }
            }
            _dirtyChunks.Clear();
        }

        void UpdateVisibleChunks()
        {
            _visibleChunkPositions.Clear();
            
            Vector3 camPos = mainCamera.transform.position;
            
            // Update cached frustum planes only if camera has moved significantly
            if (Vector3.Distance(_lastFrustumUpdatePos, camPos) > 1f || 
                Quaternion.Angle(_lastFrustumUpdateRot, mainCamera.transform.rotation) > 1f)
            {
                GeometryUtility.CalculateFrustumPlanes(mainCamera, _cachedFrustumPlanes);
                _lastFrustumUpdatePos = camPos;
                _lastFrustumUpdateRot = mainCamera.transform.rotation;
            }
            
            // Calculate visible area based on orthographicSize
            float orthoSize = mainCamera.orthographicSize;
            float aspectRatio = mainCamera.aspect;
            float visibleWidth = orthoSize * 2 * aspectRatio;
            float visibleHeight = orthoSize * 2;

            // Add buffer for chunk loading
            float buffer = chunkLoadBuffer;
            visibleWidth *= buffer;
            visibleHeight *= buffer;
            
            // Calculate bounds in chunk coordinates with extra padding
            int minX = Mathf.FloorToInt((camPos.x - visibleWidth) / ChunkData.SIZE) - 1;
            int maxX = Mathf.CeilToInt((camPos.x + visibleWidth) / ChunkData.SIZE) + 1;
            int minZ = Mathf.FloorToInt((camPos.z - visibleHeight) / ChunkData.SIZE) - 1;
            int maxZ = Mathf.CeilToInt((camPos.z + visibleHeight) / ChunkData.SIZE) + 1;

            // Check chunks within the calculated bounds
            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    CheckChunkVisibility(new int2(x, z), loadDistance * loadDistance, camPos);
                }
            }

            // Debug output if no chunks are visible
            if (_visibleChunkPositions.Count == 0)
            {
                Debug.LogWarning($"No visible chunks! Camera pos: {camPos}, Ortho size: {orthoSize}");
            }
        }

        private void CheckChunkVisibility(int2 chunkPos, float maxViewDistanceSq, Vector3 camPos)
        {
            float worldX = chunkPos.x * ChunkData.SIZE + ChunkData.SIZE * 0.5f;
            float worldZ = chunkPos.y * ChunkData.SIZE + ChunkData.SIZE * 0.5f;
            
            // Calculate bounds based on viewMaxYLevel
            float visibleHeight = Mathf.Min(viewMaxYLevel + 1, ChunkData.HEIGHT);
            float centerY = visibleHeight * 0.5f;
            
            // Create bounds that only encompasses the visible portion of the chunk
            var bounds = new Bounds(
                new Vector3(worldX, centerY, worldZ),
                new Vector3(
                    ChunkData.SIZE,
                    visibleHeight,
                    ChunkData.SIZE
                )
            );

            // Add some padding to the bounds to prevent pop-in
            bounds.Expand(new Vector3(1f, 1f, 1f));

            // If the chunk is within view frustum, add it to visible chunks
            if (GeometryUtility.TestPlanesAABB(_cachedFrustumPlanes, bounds))
            {
                _visibleChunkPositions.Add(chunkPos);
            }
        }

        // Add helper method to determine if a chunk should be rendered at current camera angle
        private bool ShouldRenderChunkAtAngle(int2 chunkPos, Vector3 camPos, Vector3 camForward)
        {
            Vector3 chunkCenter = new Vector3(
                (chunkPos.x + 0.5f) * Data.ChunkData.SIZE,
                0,
                (chunkPos.y + 0.5f) * Data.ChunkData.SIZE
            );
            
            // Direction from camera to chunk
            Vector3 toChunk = chunkCenter - camPos;
            toChunk.y = 0;
            toChunk.Normalize();
            
            // Dot product to check if chunk is in front of camera
            float dot = Vector3.Dot(camForward, toChunk);
            
            // Allow some chunks behind camera based on angle threshold
            return dot > -0.5f; // Adjust threshold as needed
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
                else if (!_worldGenerator.IsGenerating(pos) && !_chunkLoadQueue.Contains(pos))
                {
                    float priority = Vector2.Distance(
                        new Vector2(mainCamera.transform.position.x, mainCamera.transform.position.z),
                        new Vector2(pos.x * Data.ChunkData.SIZE, pos.y * Data.ChunkData.SIZE)
                    );
                    _chunkLoadQueue.Enqueue(pos, priority);
                }
            }
        }

        void OnChunkGenerated(Data.ChunkData chunk)
        {
            int2 position2D = new int2(chunk.position.x, chunk.position.z);
            
            // Clean up old heightmap if it exists
            if (_chunkHeightMaps.TryGetValue(position2D, out var oldHeightMap))
            {
                if (oldHeightMap.IsCreated)
                    oldHeightMap.Dispose();
            }
            
            // Store new heightmap
            _chunkHeightMaps[position2D] = chunk.heightMap;

            if (!_generatedChunks.Contains(position2D))
            {
                var permanentBlocks = new NativeArray<byte>(chunk.blocks.Length, 
                    Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                permanentBlocks.CopyFrom(chunk.blocks);
                
                _chunkBlockData[position2D] = permanentBlocks;
                _generatedChunks.Add(position2D);
            }

            if (!_generatedYLevels.ContainsKey(position2D))
            {
                _generatedYLevels[position2D] = new HashSet<int>();
            }
            _generatedYLevels[position2D].Add(viewMaxYLevel);

            CreateChunkObject(position2D, chunk.blocks, default);

            // Invoke the OnChunkLoaded event
            OnChunkLoaded?.Invoke(position2D);
        }

        void ProcessChunkQueue()
        {
            // Create a combined dependency for all completed jobs
            JobHandle combinedHandle = default;
            var completedJobs = new List<(int2 pos, Data.ChunkData data)>();
            
            foreach (var kvp in _pendingJobs)
            {
                if (kvp.Value.handle.IsCompleted)
                {
                    // Combine job handles instead of completing them individually
                    combinedHandle = JobHandle.CombineDependencies(combinedHandle, kvp.Value.handle);
                    
                    var chunkData = new Data.ChunkData
                    {
                        position = new int3(kvp.Key.x, viewMaxYLevel, kvp.Key.y),
                        blocks = kvp.Value.blocks,
                        isEdited = false
                    };
                    
                    completedJobs.Add((kvp.Key, chunkData));
                }
            }

            // Complete all jobs at once
            if (combinedHandle.IsCompleted)
            {
                combinedHandle.Complete();
                
                // Now process all completed chunks
                foreach (var (pos, chunkData) in completedJobs)
                {
                    OnChunkGenerated(chunkData);
                    _pendingJobs.Remove(pos);
                }
            }
            
            // Schedule new jobs
            while (_chunkLoadQueue.Count > 0 && _pendingJobs.Count < 8)
            {
                var pos = _chunkLoadQueue.Dequeue();
                if (!_pendingJobs.ContainsKey(pos) && !_generatedChunks.Contains(pos))
                {
                    // Use Persistent allocator instead of TempJob
                    var blocks = new NativeArray<byte>(
                        Data.ChunkData.SIZE * Data.ChunkData.SIZE * Data.ChunkData.HEIGHT, 
                        Allocator.Persistent);
                    var heightMap = new NativeArray<Core.HeightPoint>(
                        Data.ChunkData.SIZE * Data.ChunkData.SIZE, 
                        Allocator.Persistent);
                        
                    var handle = _worldGenerator.GenerateChunkAsync(
                        new int3(pos.x, viewMaxYLevel, pos.y),
                        blocks,
                        heightMap,
                        null
                    );
                    
                    _pendingJobs.Add(pos, (handle, blocks, heightMap));
                }
            }
        }

        void CreateChunkObject(int2 position, NativeArray<byte> blocks, JobHandle dependency)
        {
            var chunkResult = _chunkPool.GetChunk(position, viewMaxYLevel);
            if (chunkResult == null) return;

            var (chunk, meshFilter, shadowMeshFilter) = chunkResult.Value;
            _meshManager.QueueMeshBuild(position, blocks, meshFilter, shadowMeshFilter, viewMaxYLevel);
        }

        void CleanupDistantChunks()
        {
            var activeChunkPositions = _chunkPool.GetActiveChunkPositions().ToList();
            
            // Immediately deactivate any chunks not in the current view frustum
            foreach (var (pos, yLevel) in activeChunkPositions)
            {
                if (!_visibleChunkPositions.Contains(pos))
                {
                    _chunkPool.DeactivateChunk(pos, yLevel);
                    OnChunkUnloaded?.Invoke(pos);
                }
            }

            // Clear the load queue of any chunks that are no longer visible
            var newQueue = new PriorityQueue<int2>();
            while (_chunkLoadQueue.Count > 0)
            {
                var pos = _chunkLoadQueue.Dequeue();
                if (_visibleChunkPositions.Contains(pos))
                {
                    newQueue.Enqueue(pos, Vector2.Distance(
                        new Vector2(mainCamera.transform.position.x, mainCamera.transform.position.z),
                        new Vector2(pos.x * Data.ChunkData.SIZE, pos.y * Data.ChunkData.SIZE)
                    ));
                }
            }
            _chunkLoadQueue = newQueue;

            _chunkPool.CheckBufferTimeout();
            CleanupExcessChunks();
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
                    if (!_worldGenerator.IsGenerating(pos) && !_chunkLoadQueue.Contains(pos))
                    {
                        float priority = Vector2.Distance(
                            new Vector2(mainCamera.transform.position.x, mainCamera.transform.position.z),
                            new Vector2(pos.x * Data.ChunkData.SIZE, pos.y * Data.ChunkData.SIZE)
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
                Mathf.FloorToInt(camPos.x / Data.ChunkData.SIZE),
                Mathf.FloorToInt(camPos.z / Data.ChunkData.SIZE)
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
                
                // Clean up block data
                if (_chunkBlockData.TryGetValue(chunkPos, out var blocks))
                {
                    if (blocks.IsCreated)
                        blocks.Dispose();
                    _chunkBlockData.Remove(chunkPos);
                }

                // Clean up heightmap data
                if (_chunkHeightMaps.TryGetValue(chunkPos, out var heightMap))
                {
                    if (heightMap.IsCreated)
                        heightMap.Dispose();
                    _chunkHeightMaps.Remove(chunkPos);
                }

                _generatedChunks.Remove(chunkPos);
                _generatedYLevels.Remove(chunkPos);
            }
        }

        private void Start()
        {
            chunkMaterial.SetFloat("_WorldSeed", _worldGenerator.seed);
            chunkMaterial.SetFloat("_ColorVariationStrength", 0.02f);
            chunkMaterial.SetFloat("_ColorVariationScale", 1f);
        }

        public Data.ChunkData GetChunk(int2 position)
        {
            // First check if we have the chunk data in memory
            if (_chunkBlockData.TryGetValue(position, out var blocks))
            {
                return CreateChunkData(position, blocks);
            }

            // If not in memory but generated, try to get from pool
            if (_generatedChunks.Contains(position))
            {
                var pooledChunk = _chunkPool.GetChunk(position, viewMaxYLevel);
                if (pooledChunk.HasValue)
                {
                    return CreateChunkData(position, _chunkBlockData[position]);
                }
            }

            // If chunk isn't loaded yet, return an empty chunk and queue generation
            if (!_worldGenerator.IsGenerating(position) && !_chunkLoadQueue.Contains(position))
            {
                float priority = Vector2.Distance(
                    new Vector2(mainCamera.transform.position.x, mainCamera.transform.position.z),
                    new Vector2(position.x * Data.ChunkData.SIZE, position.y * Data.ChunkData.SIZE)
                );
                _chunkLoadQueue.Enqueue(position, priority);
                
                // Return empty chunk while generation is pending
                return CreateEmptyChunkData(position);
            }

            // Return empty chunk while waiting for generation
            return CreateEmptyChunkData(position);
        }

        private ChunkData CreateEmptyChunkData(int2 position)
        {
            var emptyBlocks = new NativeArray<byte>(
                ChunkData.SIZE * ChunkData.SIZE * ChunkData.HEIGHT, 
                Allocator.Persistent);
            var emptyHeightMap = new NativeArray<HeightPoint>(
                ChunkData.SIZE * ChunkData.SIZE, 
                Allocator.Persistent);
            
            return new ChunkData
            {
                position = new int3(position.x, viewMaxYLevel, position.y),
                blocks = emptyBlocks,
                heightMap = emptyHeightMap,
                isEdited = false
            };
        }

        public void ModifyBlock(int2 chunkPos, int blockIndex, byte newBlockType)
        {
            _persistenceManager.MarkBlockModified(chunkPos, blockIndex, newBlockType);
            
            if (_chunkBlockData.TryGetValue(chunkPos, out var blocks))
            {
                blocks[blockIndex] = newBlockType;
                _dirtyChunks.Add(chunkPos); // Mark chunk as dirty instead of immediate rebuild
            }
        }

        private async Task<ChunkData> GetOrGenerateChunk(int2 position)
        {
            // 1. Check memory first (fastest)
            if (_chunkBlockData.TryGetValue(position, out var blocks))
            {
                return CreateChunkData(position, blocks);
            }

            // 2. Check object pool
            var pooledChunk = _chunkPool.GetChunk(position, viewMaxYLevel);
            if (pooledChunk.HasValue)
            {
                // Convert pooled chunk to ChunkData
                var (_, meshFilter, _) = pooledChunk.Value;
                if (meshFilter.mesh != null)
                {
                    return CreateChunkData(position, _chunkBlockData[position]);
                }
            }

            // 3. Check disk
            var savedChunk = await _persistenceManager.LoadChunkAsync(position);
            if (savedChunk != null)
            {
                // Convert saved data to ChunkData and create a new NativeArray from the byte array
                var nativeBlocks = new NativeArray<byte>(savedChunk.blocks.Length, Allocator.Persistent);
                nativeBlocks.CopyFrom(savedChunk.blocks);
                
                var loadedChunk = CreateChunkData(position, nativeBlocks);
                _chunkBlockData[position] = nativeBlocks;
                _generatedChunks.Add(position);
                return loadedChunk;
            }

            // 4. Only generate if we don't have it anywhere
            return await GenerateNewChunkAsync(position);
        }

        private ChunkData CreateChunkData(int2 position, NativeArray<byte> blocks)
        {
            return new ChunkData
            {
                position = new int3(position.x, viewMaxYLevel, position.y),
                blocks = blocks,
                heightMap = _chunkHeightMaps.ContainsKey(position) ? 
                    _chunkHeightMaps[position] : 
                    new NativeArray<HeightPoint>(ChunkData.SIZE * ChunkData.SIZE, Allocator.Persistent),
                isEdited = false
            };
        }

        private async Task<ChunkData> GenerateNewChunkAsync(int2 position)
        {
            var blocks = new NativeArray<byte>(
                ChunkData.SIZE * ChunkData.SIZE * ChunkData.HEIGHT, 
                Allocator.Persistent);
            var heightMap = new NativeArray<Core.HeightPoint>(
                ChunkData.SIZE * ChunkData.SIZE, 
                Allocator.Persistent);

            var chunkPos = new int3(position.x, viewMaxYLevel, position.y);
            
            // Generate the chunk
            var tcs = new TaskCompletionSource<ChunkData>();
            
            _worldGenerator.GenerateChunkAsync(
                chunkPos,
                blocks,
                heightMap,
                (chunk) => {
                    tcs.SetResult(chunk);
                }
            );

            var newChunk = await tcs.Task;
            
            // Save the newly generated chunk immediately
            await _persistenceManager.SaveNewChunkAsync(position, newChunk);
            
            // Cache in memory
            _chunkBlockData[position] = newChunk.blocks;
            _generatedChunks.Add(position);
            
            return newChunk;
        }

        public bool IsInitialLoadComplete()
        {
            // Check if spawn chunks around origin are loaded
            int spawnRadius = 2; // Adjust based on your needs
            
            for (int x = -spawnRadius; x <= spawnRadius; x++)
            {
                for (int z = -spawnRadius; z <= spawnRadius; z++)
                {
                    if (!IsChunkLoaded(new int2(x, z)))
                    {
                        return false;
                    }
                }
            }
            
            return true;
        }

        public bool IsChunkLoaded(int2 chunkPos)
        {
            var chunk = GetChunk(chunkPos);
            return chunk.blocks.IsCreated && chunk.blocks.Length > 0;
        }

        public bool IsBlockSolid(int3 position)
        {
            // Use the existing GetBlockTypeAsync method and check if it's not Air (0)
            return GetBlockTypeAsync(position).Result != 0;
        }

        public async Task<bool> IsBlockSolidAsync(int3 position)
        {
            byte blockType = await GetBlockTypeAsync(position);
            return blockType != 0;
        }

        public bool CanStandAt(int3 position)
        {
            // Check if the position and one above it are air, and the position below is solid
            return !IsBlockSolid(position) && 
                   !IsBlockSolid(position + new int3(0, 1, 0)) && 
                   IsBlockSolid(position + new int3(0, -1, 0));
        }

        private int2 GetChunkPosition(int3 position)
        {
            // Use FloorToInt instead of regular division to handle negative coordinates correctly
            return new int2(
                Mathf.FloorToInt((float)position.x / ChunkData.SIZE),
                Mathf.FloorToInt((float)position.z / ChunkData.SIZE)
            );
        }

        private int GetLocalIndex(int3 position)
        {
            int localX = position.x % ChunkData.SIZE;
            int localZ = position.z % ChunkData.SIZE;
            if (localX < 0) localX += ChunkData.SIZE;
            if (localZ < 0) localZ += ChunkData.SIZE;
            return (position.y * ChunkData.SIZE * ChunkData.SIZE) + (localZ * ChunkData.SIZE) + localX;
        }

        public async Task<byte> GetBlockTypeAsync(int3 position)
        {
            int2 chunkPos = GetChunkPosition(position);
            int localIndex = GetLocalIndex(position);

            var chunk = await GetOrGenerateChunk(chunkPos);
            if (chunk.blocks.IsCreated && localIndex < chunk.blocks.Length)
            {
                return chunk.blocks[localIndex];
            }
            return 0; // Air block
        }

        public async Task<bool> SetBlockAsync(int3 position, byte blockType)
        {
            int2 chunkPos = GetChunkPosition(position);
            int localIndex = GetLocalIndex(position);

            var chunk = await GetOrGenerateChunk(chunkPos);
            if (chunk.blocks.IsCreated && localIndex < chunk.blocks.Length)
            {
                ModifyBlock(chunkPos, localIndex, blockType);
                return true;
            }
            return false;
        }

        public class LRUCache<TKey, TValue>
        {
            private readonly int _capacity;
            private readonly Dictionary<TKey, LinkedListNode<CacheItem>> _cache;
            private readonly LinkedList<CacheItem> _lruList;

            private struct CacheItem
            {
                public TKey Key;
                public TValue Value;
            }
        }

        public void Dispose()
        {
            foreach (var job in _pendingJobs.Values)
            {
                job.handle.Complete();
                if (job.heightMap.IsCreated) job.heightMap.Dispose();
                if (job.blocks.IsCreated) job.blocks.Dispose();
            }
            
            foreach (var job in _pendingConversionJobs.Values)
            {
                job.handle.Complete();
                if (job.heightMap.IsCreated) job.heightMap.Dispose();
                if (job.blocks.IsCreated) job.blocks.Dispose();
            }
            
            _pendingJobs.Clear();
            _pendingConversionJobs.Clear();
        }
    }
} 