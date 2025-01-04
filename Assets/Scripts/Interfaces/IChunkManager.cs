using UnityEngine;
using System.Collections.Generic;

namespace VoxelGame.Interfaces
{
    public interface IChunkManager
    {
        Chunk GetChunk(Vector3Int position);
        void StoreChunk(Vector3Int position, Chunk chunk);
        void RemoveChunk(Vector3Int position);
        bool ChunkExists(Vector3Int position);
        void MarkForUnloading(Vector3Int position);
        void UnloadMarkedChunks();
        bool IsMarkedForUnloading(Vector3Int position);
        ICollection<Vector3Int> GetAllChunkPositions();
    }
} 