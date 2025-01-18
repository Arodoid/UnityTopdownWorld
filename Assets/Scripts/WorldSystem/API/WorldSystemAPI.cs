using UnityEngine;
using Unity.Mathematics;
using System;
using System.Threading.Tasks;

namespace WorldSystem.API
{
    public class WorldSystemAPI
    {
        private readonly ChunkManager _chunkManager;

        // Simple data class for world generation settings
        public class WorldSettings
        {
            public bool Enable3DTerrain { get; set; } = true;
            public bool EnableWater { get; set; } = true;
            public float SeaLevel { get; set; } = 64f;
            public float OceanThreshold { get; set; } = 0.45f;
            public int Seed { get; set; } = 42;
        }

        public WorldSystemAPI(ChunkManager chunkManager)
        {
            _chunkManager = chunkManager;
        }

        // World Management
        public async Task<bool> LoadWorld(string worldName)
        {
            try
            {
                await _chunkManager.Initialize(worldName);
                OnWorldLoaded?.Invoke(worldName);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load world: {e.Message}");
                return false;
            }
        }

        public async Task<bool> CreateWorld(string worldName, WorldSettings settings)
        {
            try
            {
                await _chunkManager.Initialize(worldName, settings);
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
            await _chunkManager.SaveAllChunks();
        }

        // Block Operations
        public byte GetBlockType(Vector3 position)
        {
            return _chunkManager.GetBlockType(new int3(
                Mathf.FloorToInt(position.x),
                Mathf.FloorToInt(position.y),
                Mathf.FloorToInt(position.z)
            ));
        }

        public bool IsBlockSolid(Vector3 position)
        {
            byte blockType = GetBlockType(position);
            return blockType != 0 && blockType != 1; // 0 = Air, 1 = Water
        }

        public async Task<bool> ModifyBlock(Vector3 position, byte blockType)
        {
            var blockPos = new int3(
                Mathf.FloorToInt(position.x),
                Mathf.FloorToInt(position.y),
                Mathf.FloorToInt(position.z)
            );

            try
            {
                _chunkManager.ModifyBlock(
                    new int2(blockPos.x >> 5, blockPos.z >> 5), // chunk position
                    (blockPos.x & 31) + ((blockPos.z & 31) * 32) + (blockPos.y * 1024), // block index
                    blockType
                );
                OnBlockModified?.Invoke(position, blockType);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to modify block: {e.Message}");
                return false;
            }
        }

        // World Information
        public bool IsWorldLoaded => _chunkManager.IsInitialLoadComplete();
        public float LoadProgress => _chunkManager.GetLoadProgress();
        public int ActiveChunkCount => _chunkManager.ChunkPool.TotalChunksInUse;

        // Events
        public event Action<Vector3, byte> OnBlockModified;
        public event Action<string> OnWorldLoaded;
        public event Action OnWorldUnloaded;
    }
} 