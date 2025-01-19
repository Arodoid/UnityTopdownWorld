using UnityEngine;
using Unity.Mathematics;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using WorldSystem.Base;
using WorldSystem.Data;
using WorldSystem.Persistence;
using WorldSystem.Generation;

namespace WorldSystem.Implementation
{
    internal class WorldSystemImpl
    {
        private readonly object _lock = new object();
        private ChunkManager _chunkManager;
        private WorldMetadata _currentWorldMetadata;
        private WorldGenSettings _currentSettings;
        private bool _isDisposed;
        private readonly Queue<float> _loadTimes = new Queue<float>();
        private const int LOAD_TIME_SAMPLE_SIZE = 50;

        // Events
        public event Action<int3, BlockType> OnBlockModified;
        public event Action<int2> OnChunkLoaded;
        public event Action<int2> OnChunkUnloaded;
        public event Action<string> OnWorldLoaded;
        public event Action OnWorldUnloaded;

        // Properties
        public WorldMetadata CurrentWorldMetadata => _currentWorldMetadata;
        public bool IsWorldLoaded => _currentWorldMetadata != null && _chunkManager != null;
        public WorldGenSettings CurrentWorldSettings => _currentSettings;
        
        public int ActiveChunkCount => _chunkManager?.ChunkPool.TotalChunksInUse ?? 0;
        public int CachedChunkCount => _chunkManager?.GetType().GetField("_chunkBlockData", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(_chunkManager) is Dictionary<int2, Unity.Collections.NativeArray<byte>> dict 
            ? dict.Count : 0;
        
        public float ChunkLoadTimeAverage
        {
            get
            {
                if (_loadTimes.Count == 0) return 0;
                float sum = 0;
                foreach (var time in _loadTimes)
                    sum += time;
                return sum / _loadTimes.Count;
            }
        }

        public WorldSystemImpl(WorldGenSettings defaultSettings, ChunkManager existingChunkManager)
        {
            _currentSettings = defaultSettings;
            _chunkManager = existingChunkManager;
            
            // Subscribe to events
            _chunkManager.OnChunkLoaded += OnChunkManagerLoaded;
            _chunkManager.OnChunkUnloaded += OnChunkManagerUnloaded;
        }

        public async Task<bool> LoadWorld(string worldName)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(WorldSystemImpl));

            try
            {
                UnloadWorld(); // Clean up existing world if any

                _currentWorldMetadata = WorldManager.LoadWorld(worldName);
                if (_currentWorldMetadata == null) return false;

                // Initialize existing chunk manager
                var startTime = Time.realtimeSinceStartup;
                _chunkManager.Initialize(_currentSettings);

                // Wait for initial chunks to load
                while (!_chunkManager.IsInitialLoadComplete())
                {
                    await Task.Delay(100);
                }

                RecordLoadTime(Time.realtimeSinceStartup - startTime);
                OnWorldLoaded?.Invoke(worldName);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load world: {e.Message}");
                return false;
            }
        }

        public async Task<bool> CreateWorld(string worldName, WorldGenSettings settings)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(WorldSystemImpl));

            try
            {
                _currentWorldMetadata = WorldManager.CreateWorld(worldName, settings.Seed);
                _currentSettings = settings;
                return await LoadWorld(worldName);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to create world: {e.Message}");
                return false;
            }
        }

        public async Task SaveWorld()
        {
            if (!IsWorldLoaded) return;

            try
            {
                await Task.Run(() => {
                    lock (_lock)
                    {
                        var persistenceManager = _chunkManager.GetComponent<ChunkPersistenceManager>();
                        if (persistenceManager != null)
                        {
                            persistenceManager.SaveAll();
                        }
                        WorldManager.UpdateLastPlayed(_currentWorldMetadata.worldName);
                    }
                });
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to save world: {e.Message}");
            }
        }

        public void UnloadWorld()
        {
            if (!IsWorldLoaded) return;

            lock (_lock)
            {
                if (_chunkManager != null)
                {
                    // Unsubscribe from events
                    _chunkManager.OnChunkLoaded -= OnChunkManagerLoaded;
                    _chunkManager.OnChunkUnloaded -= OnChunkManagerUnloaded;
                    
                    UnityEngine.Object.Destroy(_chunkManager.gameObject);
                    _chunkManager = null;
                }
                
                _currentWorldMetadata = null;
                OnWorldUnloaded?.Invoke();
            }
        }

        public byte GetBlockType(int3 position)
        {
            if (!IsWorldLoaded) return 0;
            
            lock (_lock)
            {
                int2 chunkPos = new int2(
                    position.x >> 5, // Divide by CHUNK_SIZE
                    position.z >> 5
                );
                
                var chunk = _chunkManager.GetChunk(chunkPos);
                if (chunk.blocks.Length == 0) return 0;

                int localX = position.x & 31; // Modulo CHUNK_SIZE
                int localZ = position.z & 31;
                int index = localX + (localZ * ChunkData.SIZE) + 
                    (position.y * ChunkData.SIZE * ChunkData.SIZE);

                return chunk.blocks[index];
            }
        }

        public bool IsBlockSolid(int3 position)
        {
            byte blockType = GetBlockType(position);
            return blockType != (byte)BlockType.Air && blockType != (byte)BlockType.Water;
        }

        public async Task<bool> ModifyBlock(int3 position, BlockType blockType)
        {
            if (!IsWorldLoaded) return false;

            try
            {
                // Run the block modification on a background thread since it involves chunk updates
                await Task.Run(() => {
                    lock (_lock)
                    {
                        int2 chunkPos = new int2(position.x >> 5, position.z >> 5);
                        int localX = position.x & 31;
                        int localZ = position.z & 31;
                        int index = localX + (localZ * ChunkData.SIZE) + 
                            (position.y * ChunkData.SIZE * ChunkData.SIZE);

                        _chunkManager.ModifyBlock(chunkPos, index, (byte)blockType);
                        OnBlockModified?.Invoke(position, blockType);
                    }
                });
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to modify block: {e.Message}");
                return false;
            }
        }

        public bool CanStandAt(int3 position)
        {
            if (!IsWorldLoaded) return false;
            
            bool feetClear = !IsBlockSolid(position);
            bool headClear = !IsBlockSolid(position + new int3(0, 1, 0));
            bool hasGround = IsBlockSolid(position + new int3(0, -1, 0));
            
            return feetClear && headClear && hasGround;
        }

        public int GetHighestSolidBlock(int x, int z)
        {
            if (!IsWorldLoaded) return -1;

            int2 chunkPos = new int2(x >> 5, z >> 5);
            var chunk = _chunkManager.GetChunk(chunkPos);
            if (chunk.blocks.Length == 0) return -1;

            int localX = x & 31;
            int localZ = z & 31;

            if (chunk.heightMap.IsCreated)
            {
                int heightMapIndex = localX + (localZ * ChunkData.SIZE);
                return chunk.heightMap[heightMapIndex].height;
            }

            for (int y = ChunkData.HEIGHT - 1; y >= 0; y--)
            {
                int index = localX + (localZ * ChunkData.SIZE) + 
                    (y * ChunkData.SIZE * ChunkData.SIZE);
                if (chunk.blocks[index] != (byte)BlockType.Air)
                {
                    return y;
                }
            }
            return -1;
        }

        public bool IsPositionExposed(int3 position)
        {
            if (!IsWorldLoaded) return false;

            // Check all 6 directions
            int3[] directions = new int3[]
            {
                new int3(0, 1, 0),
                new int3(0, -1, 0),
                new int3(1, 0, 0),
                new int3(-1, 0, 0),
                new int3(0, 0, 1),
                new int3(0, 0, -1)
            };

            foreach (var dir in directions)
            {
                if (GetBlockType(position + dir) == (byte)BlockType.Air)
                    return true;
            }
            return false;
        }

        public bool IsChunkLoaded(int2 chunkPosition)
        {
            return IsWorldLoaded && _chunkManager.IsChunkLoaded(chunkPosition);
        }

        public bool IsInitialLoadComplete()
        {
            return IsWorldLoaded && _chunkManager.IsInitialLoadComplete();
        }

        public float GetLoadProgress()
        {
            if (!IsWorldLoaded) return 0f;
            // Implementation depends on how you track loading progress
            return IsInitialLoadComplete() ? 1f : 0.5f;
        }

        private void RecordLoadTime(float time)
        {
            _loadTimes.Enqueue(time);
            if (_loadTimes.Count > LOAD_TIME_SAMPLE_SIZE)
                _loadTimes.Dequeue();
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            UnloadWorld();
            _isDisposed = true;
        }

        // Add event invocations for chunk loading/unloading
        private void OnChunkManagerLoaded(int2 position)
        {
            OnChunkLoaded?.Invoke(position);
        }

        private void OnChunkManagerUnloaded(int2 position)
        {
            OnChunkUnloaded?.Invoke(position);
        }
    }
} 