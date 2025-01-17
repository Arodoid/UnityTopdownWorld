using Unity.Mathematics;
using System;
using System.Threading.Tasks;
using WorldSystem.Data;
using WorldSystem.Generation;

namespace WorldSystem
{
    public interface IWorldSystem : IDisposable
    {
        // World Management
        Task<bool> LoadWorld(string worldName);
        Task<bool> CreateWorld(string worldName, WorldGenSettings settings);
        Task SaveWorld();
        void UnloadWorld();
        
        // Block Operations
        byte GetBlockType(int3 position);
        bool IsBlockSolid(int3 position);
        Task<bool> ModifyBlock(int3 position, BlockType blockType);
        bool CanStandAt(int3 position);
        
        // Height Queries
        int GetHighestSolidBlock(int x, int z);
        bool IsPositionExposed(int3 position);
        
        // Chunk State
        bool IsChunkLoaded(int2 chunkPosition);
        bool IsInitialLoadComplete();
        float GetLoadProgress();
        
        // World Information
        WorldMetadata CurrentWorldMetadata { get; }
        bool IsWorldLoaded { get; }
        WorldGenSettings CurrentWorldSettings { get; }
        int ActiveChunkCount { get; }
        int CachedChunkCount { get; }
        float ChunkLoadTimeAverage { get; }
        
        // Events
        event Action<int3, BlockType> OnBlockModified;
        event Action<int2> OnChunkLoaded;
        event Action<int2> OnChunkUnloaded;
        event Action<string> OnWorldLoaded;
        event Action OnWorldUnloaded;
    }
}
