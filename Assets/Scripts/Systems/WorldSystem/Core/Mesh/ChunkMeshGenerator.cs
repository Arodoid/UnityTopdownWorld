using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
using WorldSystem.Data;

namespace WorldSystem.Mesh
{
    public static class ChunkMeshGenerator
    {
        [BurstCompile]
        private struct GenerateMeshJob : IJob
        {
            [ReadOnly] public NativeArray<byte> blocks;
            [ReadOnly] public NativeArray<float4> blockColors;
            public NativeList<float3> vertices;
            public NativeList<int> triangles;
            public NativeList<float2> uvs;
            public NativeList<float4> colors;
            public NativeList<float3> normals;
            public int maxYLevel;

            private bool IsBlockFaceVisible(int x, int y, int z, int dx, int dy, int dz)
            {
                int nx = x + dx;
                int ny = y + dy;
                int nz = z + dz;

                // Always show faces at chunk borders (x and z)
                if (nx < 0 || nx >= ChunkData.SIZE || nz < 0 || nz >= ChunkData.SIZE)
                    return true;

                // Check Y bounds separately (top/bottom of world)
                if (ny < 0 || ny >= ChunkData.HEIGHT)
                    return true;

                return blocks[(ny * ChunkData.SIZE * ChunkData.SIZE) + (nz * ChunkData.SIZE) + nx] == 0;
            }

            private bool CanMergeBlocks(int x1, int y1, int z1, int x2, int y2, int z2)
            {
                if (x1 < 0 || x1 >= ChunkData.SIZE || y1 < 0 || y1 >= ChunkData.HEIGHT || z1 < 0 || z1 >= ChunkData.SIZE ||
                    x2 < 0 || x2 >= ChunkData.SIZE || y2 < 0 || y2 >= ChunkData.HEIGHT || z2 < 0 || z2 >= ChunkData.SIZE)
                    return false;

                int idx1 = (y1 * ChunkData.SIZE * ChunkData.SIZE) + (z1 * ChunkData.SIZE) + x1;
                int idx2 = (y2 * ChunkData.SIZE * ChunkData.SIZE) + (z2 * ChunkData.SIZE) + x2;
                
                return blocks[idx1] == blocks[idx2] && blocks[idx1] != 0;
            }

            private void AddGreedyFace(int startX, int startY, int startZ, int width, int height, float3 normal, byte blockType)
            {
                int vertexStart = vertices.Length;
                float4 color = blockColors[blockType];

                // Add vertices for the merged face
                if (math.abs(normal.x) > 0)
                {
                    float x0 = startX + math.max(0, normal.x);
                    if (normal.x > 0)
                    {
                        vertices.Add(new float3(x0, startY, startZ));
                        vertices.Add(new float3(x0, startY + height, startZ));
                        vertices.Add(new float3(x0, startY + height, startZ + width));
                        vertices.Add(new float3(x0, startY, startZ + width));
                    }
                    else
                    {
                        vertices.Add(new float3(x0, startY, startZ + width));
                        vertices.Add(new float3(x0, startY + height, startZ + width));
                        vertices.Add(new float3(x0, startY + height, startZ));
                        vertices.Add(new float3(x0, startY, startZ));
                    }
                }
                else if (math.abs(normal.y) > 0)
                {
                    float y0 = startY + math.max(0, normal.y);
                    if (normal.y > 0)
                    {
                        vertices.Add(new float3(startX, y0, startZ));
                        vertices.Add(new float3(startX, y0, startZ + height));
                        vertices.Add(new float3(startX + width, y0, startZ + height));
                        vertices.Add(new float3(startX + width, y0, startZ));
                    }
                    else
                    {
                        vertices.Add(new float3(startX, y0, startZ));
                        vertices.Add(new float3(startX + width, y0, startZ));
                        vertices.Add(new float3(startX + width, y0, startZ + height));
                        vertices.Add(new float3(startX, y0, startZ + height));
                    }
                }
                else
                {
                    float z0 = startZ + math.max(0, normal.z);
                    if (normal.z > 0)
                    {
                        vertices.Add(new float3(startX + width, startY, z0));
                        vertices.Add(new float3(startX + width, startY + height, z0));
                        vertices.Add(new float3(startX, startY + height, z0));
                        vertices.Add(new float3(startX, startY, z0));
                    }
                    else
                    {
                        vertices.Add(new float3(startX, startY, z0));
                        vertices.Add(new float3(startX, startY + height, z0));
                        vertices.Add(new float3(startX + width, startY + height, z0));
                        vertices.Add(new float3(startX + width, startY, z0));
                    }
                }

                // Add UVs (potentially tiled based on width/height)
                uvs.Add(new float2(0, 0));
                uvs.Add(new float2(0, height));
                uvs.Add(new float2(width, height));
                uvs.Add(new float2(width, 0));

                // Add colors and normals
                for (int i = 0; i < 4; i++)
                {
                    colors.Add(color);
                    normals.Add(normal);
                }

                // Add triangles (same as before)
                triangles.Add(vertexStart);
                triangles.Add(vertexStart + 1);
                triangles.Add(vertexStart + 2);
                triangles.Add(vertexStart);
                triangles.Add(vertexStart + 2);
                triangles.Add(vertexStart + 3);
            }

            public void Execute()
            {
                // Replace single processed array with six separate arrays for each face direction
                bool[,,] processedXPos = new bool[ChunkData.SIZE, maxYLevel, ChunkData.SIZE];
                bool[,,] processedXNeg = new bool[ChunkData.SIZE, maxYLevel, ChunkData.SIZE];
                bool[,,] processedYPos = new bool[ChunkData.SIZE, maxYLevel, ChunkData.SIZE];
                bool[,,] processedYNeg = new bool[ChunkData.SIZE, maxYLevel, ChunkData.SIZE];
                bool[,,] processedZPos = new bool[ChunkData.SIZE, maxYLevel, ChunkData.SIZE];
                bool[,,] processedZNeg = new bool[ChunkData.SIZE, maxYLevel, ChunkData.SIZE];
                
                int facesAdded = 0;

                for (int y = 0; y < maxYLevel; y++)
                for (int z = 0; z < ChunkData.SIZE; z++)
                for (int x = 0; x < ChunkData.SIZE; x++)
                {
                    int idx = (y * ChunkData.SIZE * ChunkData.SIZE) + (z * ChunkData.SIZE) + x;
                    byte block = blocks[idx];
                    if (block == 0 || block >= blockColors.Length) continue;

                    // For each direction, check its own processed array
                    if (!processedXPos[x, y, z] && IsBlockFaceVisible(x, y, z, 1, 0, 0))
                    {
                        // Find maximum width and height for this face
                        int width = 1;
                        int height = 1;

                        // Expand width (z direction)
                        while (z + width < ChunkData.SIZE && 
                               !processedXPos[x, y, z + width] &&
                               CanMergeBlocks(x, y, z, x, y, z + width) &&
                               IsBlockFaceVisible(x, y, z + width, 1, 0, 0))
                        {
                            width++;
                        }

                        // Expand height (y direction)
                        bool canExpandHeight = true;
                        while (canExpandHeight && y + height < maxYLevel)
                        {
                            for (int dz = 0; dz < width; dz++)
                            {
                                if (processedXPos[x, y + height, z + dz] ||
                                    !CanMergeBlocks(x, y, z, x, y + height, z + dz) ||
                                    !IsBlockFaceVisible(x, y + height, z + dz, 1, 0, 0))
                                {
                                    canExpandHeight = false;
                                    break;
                                }
                            }
                            if (canExpandHeight) height++;
                        }

                        // Only mark as processed for this direction
                        for (int dy = 0; dy < height; dy++)
                        for (int dz = 0; dz < width; dz++)
                        {
                            processedXPos[x, y + dy, z + dz] = true;
                        }

                        AddGreedyFace(x, y, z, width, height, new float3(1, 0, 0), block);
                        facesAdded++;
                    }

                    if (!processedXNeg[x, y, z] && IsBlockFaceVisible(x, y, z, -1, 0, 0))
                    {
                        int width = 1;
                        int height = 1;

                        while (z + width < ChunkData.SIZE && 
                               !processedXNeg[x, y, z + width] &&
                               CanMergeBlocks(x, y, z, x, y, z + width) &&
                               IsBlockFaceVisible(x, y, z + width, -1, 0, 0))
                        {
                            width++;
                        }

                        bool canExpandHeight = true;
                        while (canExpandHeight && y + height < maxYLevel)
                        {
                            for (int dz = 0; dz < width; dz++)
                            {
                                if (processedXNeg[x, y + height, z + dz] ||
                                    !CanMergeBlocks(x, y, z, x, y + height, z + dz) ||
                                    !IsBlockFaceVisible(x, y + height, z + dz, -1, 0, 0))
                                {
                                    canExpandHeight = false;
                                    break;
                                }
                            }
                            if (canExpandHeight) height++;
                        }

                        for (int dy = 0; dy < height; dy++)
                        for (int dz = 0; dz < width; dz++)
                        {
                            processedXNeg[x, y + dy, z + dz] = true;
                        }

                        AddGreedyFace(x, y, z, width, height, new float3(-1, 0, 0), block);
                        facesAdded++;
                    }

                    if (!processedYPos[x, y, z] && IsBlockFaceVisible(x, y, z, 0, 1, 0))
                    {
                        int width = 1;
                        int depth = 1;

                        while (x + width < ChunkData.SIZE && 
                               !processedYPos[x + width, y, z] &&
                               CanMergeBlocks(x, y, z, x + width, y, z) &&
                               IsBlockFaceVisible(x + width, y, z, 0, 1, 0))
                        {
                            width++;
                        }

                        bool canExpandDepth = true;
                        while (canExpandDepth && z + depth < ChunkData.SIZE)
                        {
                            for (int dx = 0; dx < width; dx++)
                            {
                                if (processedYPos[x + dx, y, z + depth] ||
                                    !CanMergeBlocks(x, y, z, x + dx, y, z + depth) ||
                                    !IsBlockFaceVisible(x + dx, y, z + depth, 0, 1, 0))
                                {
                                    canExpandDepth = false;
                                    break;
                                }
                            }
                            if (canExpandDepth) depth++;
                        }

                        for (int dx = 0; dx < width; dx++)
                        for (int dz = 0; dz < depth; dz++)
                        {
                            processedYPos[x + dx, y, z + dz] = true;
                        }

                        AddGreedyFace(x, y, z, width, depth, new float3(0, 1, 0), block);
                        facesAdded++;
                    }

                    if (!processedYNeg[x, y, z] && IsBlockFaceVisible(x, y, z, 0, -1, 0))
                    {
                        int width = 1;
                        int depth = 1;

                        while (x + width < ChunkData.SIZE && 
                               !processedYNeg[x + width, y, z] &&
                               CanMergeBlocks(x, y, z, x + width, y, z) &&
                               IsBlockFaceVisible(x + width, y, z, 0, -1, 0))
                        {
                            width++;
                        }

                        bool canExpandDepth = true;
                        while (canExpandDepth && z + depth < ChunkData.SIZE)
                        {
                            for (int dx = 0; dx < width; dx++)
                            {
                                if (processedYNeg[x + dx, y, z + depth] ||
                                    !CanMergeBlocks(x, y, z, x + dx, y, z + depth) ||
                                    !IsBlockFaceVisible(x + dx, y, z + depth, 0, -1, 0))
                                {
                                    canExpandDepth = false;
                                    break;
                                }
                            }
                            if (canExpandDepth) depth++;
                        }

                        for (int dx = 0; dx < width; dx++)
                        for (int dz = 0; dz < depth; dz++)
                        {
                            processedYNeg[x + dx, y, z + dz] = true;
                        }

                        AddGreedyFace(x, y, z, width, depth, new float3(0, -1, 0), block);
                        facesAdded++;
                    }

                    if (!processedZPos[x, y, z] && IsBlockFaceVisible(x, y, z, 0, 0, 1))
                    {
                        int width = 1;
                        int height = 1;

                        while (x + width < ChunkData.SIZE && 
                               !processedZPos[x + width, y, z] &&
                               CanMergeBlocks(x, y, z, x + width, y, z) &&
                               IsBlockFaceVisible(x + width, y, z, 0, 0, 1))
                        {
                            width++;
                        }

                        bool canExpandHeight = true;
                        while (canExpandHeight && y + height < maxYLevel)
                        {
                            for (int dx = 0; dx < width; dx++)
                            {
                                if (processedZPos[x + dx, y + height, z] ||
                                    !CanMergeBlocks(x, y, z, x + dx, y + height, z) ||
                                    !IsBlockFaceVisible(x + dx, y + height, z, 0, 0, 1))
                                {
                                    canExpandHeight = false;
                                    break;
                                }
                            }
                            if (canExpandHeight) height++;
                        }

                        for (int dy = 0; dy < height; dy++)
                        for (int dx = 0; dx < width; dx++)
                        {
                            processedZPos[x + dx, y + dy, z] = true;
                        }

                        AddGreedyFace(x, y, z, width, height, new float3(0, 0, 1), block);
                        facesAdded++;
                    }

                    if (!processedZNeg[x, y, z] && IsBlockFaceVisible(x, y, z, 0, 0, -1))
                    {
                        int width = 1;
                        int height = 1;

                        while (x + width < ChunkData.SIZE && 
                               !processedZNeg[x + width, y, z] &&
                               CanMergeBlocks(x, y, z, x + width, y, z) &&
                               IsBlockFaceVisible(x + width, y, z, 0, 0, -1))
                        {
                            width++;
                        }

                        bool canExpandHeight = true;
                        while (canExpandHeight && y + height < maxYLevel)
                        {
                            for (int dx = 0; dx < width; dx++)
                            {
                                if (processedZNeg[x + dx, y + height, z] ||
                                    !CanMergeBlocks(x, y, z, x + dx, y + height, z) ||
                                    !IsBlockFaceVisible(x + dx, y + height, z, 0, 0, -1))
                                {
                                    canExpandHeight = false;
                                    break;
                                }
                            }
                            if (canExpandHeight) height++;
                        }

                        for (int dy = 0; dy < height; dy++)
                        for (int dx = 0; dx < width; dx++)
                        {
                            processedZNeg[x + dx, y + dy, z] = true;
                        }

                        AddGreedyFace(x, y, z, width, height, new float3(0, 0, -1), block);
                        facesAdded++;
                    }
                }

                // Debug validation
                if (facesAdded == 0)
                {
                    bool hasAnyBlocks = false;
                    for (int i = 0; i < blocks.Length; i++)
                    {
                        if (blocks[i] != 0) { hasAnyBlocks = true; break; }
                    }
                    if (hasAnyBlocks)
                    {
                        Debug.LogWarning($"No faces added but chunk contains blocks. MaxYLevel: {maxYLevel}, BlockColors: {blockColors.Length}");
                    }
                }
            }
        }

        public static JobHandle GenerateMesh(
            NativeArray<byte> blocks,
            NativeArray<float4> blockColors,
            int maxYLevel,
            out NativeList<float3> vertices,
            out NativeList<int> triangles,
            out NativeList<float2> uvs,
            out NativeList<float4> colors,
            out NativeList<float3> normals)
        {
            // Create output lists
            vertices = new NativeList<float3>(Allocator.TempJob);
            triangles = new NativeList<int>(Allocator.TempJob);
            uvs = new NativeList<float2>(Allocator.TempJob);
            colors = new NativeList<float4>(Allocator.TempJob);
            normals = new NativeList<float3>(Allocator.TempJob);

            var job = new GenerateMeshJob
            {
                blocks = blocks,
                blockColors = blockColors,
                vertices = vertices,
                triangles = triangles,
                uvs = uvs,
                colors = colors,
                normals = normals,
                maxYLevel = maxYLevel
            };

            return job.Schedule();
        }
    }
} 