using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using WorldSystem.Data;

namespace WorldSystem.Jobs
{
    [BurstCompile]
    public struct ChunkGenerationJob : IJob
    {
        // Input
        public int2 position;
        public int seed;
        
        // Output.
        public NativeArray<byte> blocks;
        public NativeArray<HeightPoint> heightMap;

        // Constants
        private const float NOISE_SCALE = 0.05f;
        private const float HEIGHT_SCALE = 8f;
        private const float BASE_HEIGHT = 8f;

        private float GetNoise(float2 pos)
        {
            float2 worldPos = new float2(
                (position.x * ChunkData.SIZE + pos.x) * NOISE_SCALE,
                (position.y * ChunkData.SIZE + pos.y) * NOISE_SCALE
            );

            return noise.snoise(worldPos) * HEIGHT_SCALE + BASE_HEIGHT;
        }

        private BlockType GetBlockType(int height, int y)
        {
            if (y > height) return BlockType.Air;
            if (y == height) return BlockType.Grass;
            if (y > height - 3) return BlockType.Dirt;
            return BlockType.Stone;
        }

        public void Execute()
        {
            for (int x = 0; x < ChunkData.SIZE; x++)
            for (int z = 0; z < ChunkData.SIZE; z++)
            {
                // Get height for this column
                float heightNoise = GetNoise(new float2(x, z));
                int height = (int)math.clamp(heightNoise, 0, ChunkData.SIZE - 1);

                // Store in heightmap
                int mapIndex = z * ChunkData.SIZE + x;
                heightMap[mapIndex] = new HeightPoint 
                { 
                    height = (byte)height,
                    blockType = (byte)GetBlockType(height, height)
                };

                // Fill full chunk data
                for (int y = 0; y < ChunkData.SIZE; y++)
                {
                    int index = (y * ChunkData.SIZE * ChunkData.SIZE) + (z * ChunkData.SIZE) + x;
                    blocks[index] = (byte)GetBlockType(height, y);
                }
            }
        }
    }

    [BurstCompile]
    public struct ChunkMeshJob : IJob
    {
        // Input
        public NativeArray<HeightPoint> heightMap;
        public int2 chunkPosition;
        [ReadOnly] public NativeArray<BlockDefinition> blockDefinitions;
        
        // Output
        public NativeArray<float3> vertices;
        public NativeArray<int> triangles;
        public NativeArray<float2> uvs;
        public NativeArray<float4> colors;

        public void Execute()
        {
            int vertexIndex = 0;
            int triangleIndex = 0;

            for (int x = 0; x < ChunkData.SIZE; x++)
            for (int z = 0; z < ChunkData.SIZE; z++)
            {
                var point = heightMap[z * ChunkData.SIZE + x];
                float y = point.height;

                // Get color from block definitions
                float4 color = blockDefinitions[point.blockType].color;

                // Add vertices for top face quad
                vertices[vertexIndex + 0] = new float3(x, y, z);
                vertices[vertexIndex + 1] = new float3(x, y, z + 1);
                vertices[vertexIndex + 2] = new float3(x + 1, y, z + 1);
                vertices[vertexIndex + 3] = new float3(x + 1, y, z);

                // Add triangles
                triangles[triangleIndex + 0] = vertexIndex + 0;
                triangles[triangleIndex + 1] = vertexIndex + 1;
                triangles[triangleIndex + 2] = vertexIndex + 2;
                triangles[triangleIndex + 3] = vertexIndex + 0;
                triangles[triangleIndex + 4] = vertexIndex + 2;
                triangles[triangleIndex + 5] = vertexIndex + 3;

                // Add UVs (simple 0-1 mapping)
                uvs[vertexIndex + 0] = new float2(0, 0);
                uvs[vertexIndex + 1] = new float2(0, 1);
                uvs[vertexIndex + 2] = new float2(1, 1);
                uvs[vertexIndex + 3] = new float2(1, 0);

                // Set colors
                colors[vertexIndex + 0] = color;
                colors[vertexIndex + 1] = color;
                colors[vertexIndex + 2] = color;
                colors[vertexIndex + 3] = color;

                vertexIndex += 4;
                triangleIndex += 6;
            }
        }
    }
} 