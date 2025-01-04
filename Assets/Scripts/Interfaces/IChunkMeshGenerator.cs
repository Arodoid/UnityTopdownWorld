using UnityEngine;

namespace VoxelGame.Interfaces
{
    public interface IChunkMeshGenerator
    {
        void GenerateMeshData(Chunk chunk, Mesh mesh, int maxYLevel);
    }
} 