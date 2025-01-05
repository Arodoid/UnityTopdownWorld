using Unity.Mathematics;
using Unity.Collections;
using Unity.Burst;

namespace VoxelGame.WorldSystem.Jobs
{
    // Simplified block data - only what we need for top faces
    public struct BlockData
    {
        public byte blockType;
        public float4 color;    // Using float4 for color
        public float2 uvStart;  // UV coordinates for top face
        public bool isOpaque;   // For determining if we need to render blocks below
    }

    // Represents a merged rectangle in greedy meshing
    public struct MergedRect
    {
        public int2 start;      // Start position (x,z)
        public int2 size;       // Width and depth of merged rectangle
        public int y;           // Height level
        public BlockData block; // Block data for the merged area
    }

    // Add LOD level enum
    public enum MeshLODLevel
    {
        High,   // Full greedy meshing
        Ultra   // Single quad
    }

    // Input data for the job
    public struct ChunkJobData
    {
        public int3 chunkPosition;
        public int chunkSize;
        public int maxYLevel;
        public NativeArray<BlockData> blocks;
        public MeshLODLevel lodLevel; // New field
    }

    // Output mesh data
    public struct MeshJobOutput
    {
        public NativeList<float3> vertices;
        public NativeList<int> triangles;
        public NativeList<float2> uvs;
        public NativeList<float4> colors;
    }
} 