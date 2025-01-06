using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using WorldSystem.Data;

namespace WorldSystem.Jobs
{
    [BurstCompile]
    public struct ChunkGenerationJob : IJobParallelFor
    {
        // Input
        [ReadOnly] public int2 position;
        [ReadOnly] public int seed;
        
        // Output
        [NativeDisableParallelForRestriction]
        public NativeArray<byte> blocks;
        [NativeDisableParallelForRestriction]
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

        public void Execute(int index)
        {
            // Convert index to x,z coordinates
            int x = index % ChunkData.SIZE;
            int z = index / ChunkData.SIZE;

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
                int blockIndex = (y * ChunkData.SIZE * ChunkData.SIZE) + (z * ChunkData.SIZE) + x;
                blocks[blockIndex] = (byte)GetBlockType(height, y);
            }
        }
    }

    [BurstCompile]
    public struct ChunkMeshJob : IJobParallelFor
    {
        // Input
        [ReadOnly] public NativeArray<HeightPoint> heightMap;
        [ReadOnly] public int2 chunkPosition;
        [ReadOnly] public NativeArray<BlockDefinition> blockDefinitions;
        
        // Output
        [NativeDisableParallelForRestriction]
        public NativeArray<float3> vertices;
        [NativeDisableParallelForRestriction]
        public NativeArray<int> triangles;
        [NativeDisableParallelForRestriction]
        public NativeArray<float2> uvs;
        [NativeDisableParallelForRestriction]
        public NativeArray<float4> colors;
        [NativeDisableParallelForRestriction]
        public NativeArray<int> meshCounts; // [vertexCount, triCount, shadowVertexCount, shadowTriCount] per row

        // Add new outputs for shadow mesh
        [NativeDisableParallelForRestriction]
        public NativeArray<float3> shadowVertices;
        [NativeDisableParallelForRestriction]
        public NativeArray<int> shadowTriangles;

        public void Execute(int index)
        {
            // We'll process one row at a time
            int z = index;
            if (z >= ChunkData.SIZE) return;

            int vertexStart = z * ChunkData.SIZE * 4; // Starting vertex index for this row
            int triStart = z * ChunkData.SIZE * 6;    // Starting triangle index for this row
            int currentVertex = vertexStart;
            int currentTri = triStart;

            int x = 0;
            while (x < ChunkData.SIZE)
            {
                var currentPoint = heightMap[z * ChunkData.SIZE + x];
                if (currentPoint.blockType == 0) // Air block
                {
                    x++;
                    continue;
                }

                // Find how many similar blocks we can combine
                int width = 1;
                while (x + width < ChunkData.SIZE)
                {
                    var nextPoint = heightMap[z * ChunkData.SIZE + x + width];
                    if (nextPoint.blockType != currentPoint.blockType || 
                        nextPoint.height != currentPoint.height)
                        break;
                    width++;
                }

                // Create a quad for this strip
                float4 color = blockDefinitions[currentPoint.blockType].color;
                float y = currentPoint.height;

                // Add vertices for the merged quad
                vertices[currentVertex + 0] = new float3(x, y, z);
                vertices[currentVertex + 1] = new float3(x, y, z + 1);
                vertices[currentVertex + 2] = new float3(x + width, y, z + 1);
                vertices[currentVertex + 3] = new float3(x + width, y, z);

                // Add triangles
                triangles[currentTri + 0] = currentVertex + 0;
                triangles[currentTri + 1] = currentVertex + 1;
                triangles[currentTri + 2] = currentVertex + 2;
                triangles[currentTri + 3] = currentVertex + 0;
                triangles[currentTri + 4] = currentVertex + 2;
                triangles[currentTri + 5] = currentVertex + 3;

                // Add UVs (tiled based on width)
                uvs[currentVertex + 0] = new float2(0, 0);
                uvs[currentVertex + 1] = new float2(0, 1);
                uvs[currentVertex + 2] = new float2(width, 1);
                uvs[currentVertex + 3] = new float2(width, 0);

                // Add colors
                colors[currentVertex + 0] = color;
                colors[currentVertex + 1] = color;
                colors[currentVertex + 2] = color;
                colors[currentVertex + 3] = color;

                currentVertex += 4;
                currentTri += 6;
                x += width;
            }

            // Second pass: Create connecting faces for height differences
            int shadowVertexStart = z * ChunkData.SIZE * 16; // 4 verts per face * 4 possible faces (N/S/E/W)
            int shadowTriStart = z * ChunkData.SIZE * 24;    // 6 tris per face * 4 possible faces
            int currentShadowVertex = shadowVertexStart;
            int currentShadowTri = shadowTriStart;

            x = 0;
            while (x < ChunkData.SIZE)
            {
                var currentPoint = heightMap[z * ChunkData.SIZE + x];
                if (currentPoint.blockType == 0)
                {
                    x++;
                    continue;
                }

                // Check West neighbor (X-)
                if (x > 0)
                {
                    var westPoint = heightMap[z * ChunkData.SIZE + x - 1];
                    if (westPoint.height < currentPoint.height)
                    {
                        shadowVertices[currentShadowVertex + 0] = new float3(x, currentPoint.height, z);
                        shadowVertices[currentShadowVertex + 1] = new float3(x, westPoint.height, z);
                        shadowVertices[currentShadowVertex + 2] = new float3(x, westPoint.height, z + 1);
                        shadowVertices[currentShadowVertex + 3] = new float3(x, currentPoint.height, z + 1);

                        AddShadowQuadTriangles(ref currentShadowTri, currentShadowVertex);
                        currentShadowVertex += 4;
                    }
                }

                // Check East neighbor (X+)
                if (x < ChunkData.SIZE - 1)
                {
                    var eastPoint = heightMap[z * ChunkData.SIZE + x + 1];
                    if (eastPoint.height < currentPoint.height)
                    {
                        // Fixed winding order for East face
                        shadowVertices[currentShadowVertex + 0] = new float3(x + 1, currentPoint.height, z + 1);
                        shadowVertices[currentShadowVertex + 1] = new float3(x + 1, eastPoint.height, z + 1);
                        shadowVertices[currentShadowVertex + 2] = new float3(x + 1, eastPoint.height, z);
                        shadowVertices[currentShadowVertex + 3] = new float3(x + 1, currentPoint.height, z);

                        AddShadowQuadTriangles(ref currentShadowTri, currentShadowVertex);
                        currentShadowVertex += 4;
                    }
                }

                // Check North neighbor (Z-)
                if (z > 0)
                {
                    var northPoint = heightMap[(z - 1) * ChunkData.SIZE + x];
                    if (northPoint.height < currentPoint.height)
                    {
                        // Fixed winding order for North face
                        shadowVertices[currentShadowVertex + 0] = new float3(x + 1, currentPoint.height, z);
                        shadowVertices[currentShadowVertex + 1] = new float3(x + 1, northPoint.height, z);
                        shadowVertices[currentShadowVertex + 2] = new float3(x, northPoint.height, z);
                        shadowVertices[currentShadowVertex + 3] = new float3(x, currentPoint.height, z);

                        AddShadowQuadTriangles(ref currentShadowTri, currentShadowVertex);
                        currentShadowVertex += 4;
                    }
                }

                // Check South neighbor (existing code)
                if (z < ChunkData.SIZE - 1)
                {
                    var southPoint = heightMap[(z + 1) * ChunkData.SIZE + x];
                    if (southPoint.height < currentPoint.height)
                    {
                        shadowVertices[currentShadowVertex + 0] = new float3(x, currentPoint.height, z + 1);
                        shadowVertices[currentShadowVertex + 1] = new float3(x, southPoint.height, z + 1);
                        shadowVertices[currentShadowVertex + 2] = new float3(x + 1, southPoint.height, z + 1);
                        shadowVertices[currentShadowVertex + 3] = new float3(x + 1, currentPoint.height, z + 1);

                        AddShadowQuadTriangles(ref currentShadowTri, currentShadowVertex);
                        currentShadowVertex += 4;
                    }
                }

                x++;
            }

            // Store the counts for this row (4 values per row now)
            meshCounts[z * 4] = currentVertex - vertexStart;         // Vertices used
            meshCounts[z * 4 + 1] = currentTri - triStart;          // Triangles used
            meshCounts[z * 4 + 2] = currentShadowVertex - shadowVertexStart;  // Shadow vertices used
            meshCounts[z * 4 + 3] = currentShadowTri - shadowTriStart;       // Shadow triangles used
        }

        private void AddShadowQuadTriangles(ref int currentTri, int vertexOffset)
        {
            shadowTriangles[currentTri + 0] = vertexOffset + 0;
            shadowTriangles[currentTri + 1] = vertexOffset + 1;
            shadowTriangles[currentTri + 2] = vertexOffset + 2;
            shadowTriangles[currentTri + 3] = vertexOffset + 0;
            shadowTriangles[currentTri + 4] = vertexOffset + 2;
            shadowTriangles[currentTri + 5] = vertexOffset + 3;
            currentTri += 6;
        }
    }
}   


