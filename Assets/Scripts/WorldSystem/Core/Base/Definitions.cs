using Unity.Mathematics;
using Unity.Collections;

namespace WorldSystem.Data
{
    public enum BlockType : byte
    {
        Air = 0,
        Grass = 1,
        Dirt = 2,
        Stone = 3,
        Sand = 4
    }

    public struct BlockDefinition
    {
        public float4 color;
        public bool isOpaque;
    }

    public struct HeightPoint
    {
        public byte height;
        public byte blockType;
    }

    public struct ChunkData
    {
        public const int SIZE = 16;
        public int2 position;
        public bool isEdited;
        public NativeArray<byte> blocks;
        public NativeArray<HeightPoint> heightMap;
    }

    public struct MeshData
    {
        public NativeArray<float3> vertices;
        public NativeArray<int> triangles;
        public NativeArray<float2> uvs;
        public NativeArray<float4> colors;
        public int2 position;
    }

    public static class BlockColors
    {
        public static readonly BlockDefinition[] Definitions = new BlockDefinition[]
        {
            // Air
            new BlockDefinition { 
                color = new float4(0, 0, 0, 0), 
                isOpaque = false 
            },
            // Grass
            new BlockDefinition { 
                color = new float4(0.4f, 0.7f, 0.3f, 1f), 
                isOpaque = true 
            },
            // Dirt
            new BlockDefinition { 
                color = new float4(0.6f, 0.4f, 0.2f, 1f), 
                isOpaque = true 
            },
            // Stone
            new BlockDefinition { 
                color = new float4(0.5f, 0.5f, 0.5f, 1f), 
                isOpaque = true 
            },
            // Sand
            new BlockDefinition { 
                color = new float4(0.9f, 0.9f, 0.7f, 1f), 
                isOpaque = true 
            }
        };
    }
} 