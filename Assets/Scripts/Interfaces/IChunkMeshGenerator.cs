using UnityEngine;

namespace VoxelGame.Interfaces
{
    public interface IChunkMeshGenerator
    {
        /// <summary>
        /// Attempts to generate mesh data for a chunk. May reject if too busy.
        /// </summary>
        /// <param name="chunk">The chunk to generate mesh for</param>
        /// <param name="mesh">Optional mesh to reuse</param>
        /// <param name="maxYLevel">Maximum Y level to generate</param>
        /// <returns>True if mesh generation was scheduled, false if rejected</returns>
        bool GenerateMeshData(Chunk chunk, Mesh mesh, int maxYLevel);
        
        bool HasPendingJob(Vector3Int chunkPos);
        void CancelJob(Vector3Int chunkPos);
    }
} 