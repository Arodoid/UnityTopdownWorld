using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using WorldSystem.Data;

namespace WorldSystem.Jobs
{
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
        public NativeArray<int> meshCounts;

        // Shadow mesh outputs
        [NativeDisableParallelForRestriction]
        public NativeArray<float3> shadowVertices;
        [NativeDisableParallelForRestriction]
        public NativeArray<int> shadowTriangles;
        [NativeDisableParallelForRestriction]
        public NativeArray<float3> shadowNormals;

        // Add new field for processed blocks tracking
        [NativeDisableParallelForRestriction]
        public NativeArray<bool> processed;

        public void Execute(int index)
        {
            // Skip the per-row processing since we'll handle the entire heightmap at once
            if (index != 0) return;

            int currentVertex = 0;
            int currentTri = 0;

            // Iterate through each cell
            for (int z = 0; z < ChunkData.SIZE; z++)
            {
                for (int x = 0; x < ChunkData.SIZE; x++)
                {
                    int cellIndex = z * ChunkData.SIZE + x;
                    if (processed[cellIndex]) continue;

                    var currentPoint = heightMap[cellIndex];
                    if (currentPoint.blockType == 0) // Air block
                    {
                        processed[cellIndex] = true;
                        continue;
                    }

                    // Find similar height blocks to combine in both X and Z directions
                    int width = 1;
                    int depth = 1;

                    // Expand in X direction
                    while (x + width < ChunkData.SIZE)
                    {
                        var nextPoint = heightMap[z * ChunkData.SIZE + x + width];
                        if (nextPoint.blockType != currentPoint.blockType || 
                            nextPoint.height != currentPoint.height)
                            break;
                        width++;
                    }

                    // Expand in Z direction
                    bool canExpandDepth = true;
                    while (z + depth < ChunkData.SIZE && canExpandDepth)
                    {
                        // Check entire row for matching blocks
                        for (int dx = 0; dx < width; dx++)
                        {
                            var nextPoint = heightMap[(z + depth) * ChunkData.SIZE + x + dx];
                            if (nextPoint.blockType != currentPoint.blockType || 
                                nextPoint.height != currentPoint.height)
                            {
                                canExpandDepth = false;
                                break;
                            }
                        }
                        if (canExpandDepth)
                            depth++;
                    }

                    // Mark all cells in the rectangle as processed
                    for (int dz = 0; dz < depth; dz++)
                        for (int dx = 0; dx < width; dx++)
                            processed[(z + dz) * ChunkData.SIZE + (x + dx)] = true;

                    // Create quad for this rectangle
                    float4 color = blockDefinitions[currentPoint.blockType].color;
                    AddGreedyQuad(ref currentVertex, ref currentTri, x, currentPoint.height, z, width, depth, color);
                }
            }

            ProcessShadowMesh(0, ref currentVertex, ref currentTri);
            StoreMeshCounts(0, 0, 0, currentVertex, currentTri);
        }

        private void AddGreedyQuad(ref int currentVertex, ref int currentTri, int x, float y, int z, 
            int width, int depth, float4 color)
        {
            // Add vertices for the rectangular quad
            vertices[currentVertex + 0] = new float3(x, y, z);
            vertices[currentVertex + 1] = new float3(x, y, z + depth);
            vertices[currentVertex + 2] = new float3(x + width, y, z + depth);
            vertices[currentVertex + 3] = new float3(x + width, y, z);

            // Add triangles
            triangles[currentTri + 0] = currentVertex + 0;
            triangles[currentTri + 1] = currentVertex + 1;
            triangles[currentTri + 2] = currentVertex + 2;
            triangles[currentTri + 3] = currentVertex + 0;
            triangles[currentTri + 4] = currentVertex + 2;
            triangles[currentTri + 5] = currentVertex + 3;

            // Add UVs (scaled by width and depth for proper texturing)
            uvs[currentVertex + 0] = new float2(0, 0);
            uvs[currentVertex + 1] = new float2(0, depth);
            uvs[currentVertex + 2] = new float2(width, depth);
            uvs[currentVertex + 3] = new float2(width, 0);

            // Add colors
            colors[currentVertex + 0] = color;
            colors[currentVertex + 1] = color;
            colors[currentVertex + 2] = color;
            colors[currentVertex + 3] = color;

            currentVertex += 4;
            currentTri += 6;
        }

        private void ProcessShadowMesh(int z, ref int currentVertex, ref int currentTri)
        {
            int shadowVertexStart = z * ChunkData.SIZE * 16;
            int shadowTriStart = z * ChunkData.SIZE * 24;
            int currentShadowVertex = shadowVertexStart;
            int currentShadowTri = shadowTriStart;

            for (int x = 0; x < ChunkData.SIZE; x++)
            {
                var currentPoint = heightMap[z * ChunkData.SIZE + x];
                if (currentPoint.blockType == 0) continue;

                CheckNeighborHeightDifferences(x, z, currentPoint, ref currentShadowVertex, ref currentShadowTri);
            }

            currentVertex = currentShadowVertex;
            currentTri = currentShadowTri;
        }

        private void CheckNeighborHeightDifferences(int x, int z, HeightPoint currentPoint, 
            ref int currentShadowVertex, ref int currentShadowTri)
        {
            // Check all four directions (West, East, North, South)
            CheckWestNeighbor(x, z, currentPoint, ref currentShadowVertex, ref currentShadowTri);
            CheckEastNeighbor(x, z, currentPoint, ref currentShadowVertex, ref currentShadowTri);
            CheckNorthNeighbor(x, z, currentPoint, ref currentShadowVertex, ref currentShadowTri);
            CheckSouthNeighbor(x, z, currentPoint, ref currentShadowVertex, ref currentShadowTri);
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

        private void StoreMeshCounts(int z, int vertexStart, int triStart, int currentVertex, int currentTri)
        {
            meshCounts[z * 4] = currentVertex - vertexStart;
            meshCounts[z * 4 + 1] = currentTri - triStart;
            meshCounts[z * 4 + 2] = currentVertex - vertexStart;
            meshCounts[z * 4 + 3] = currentTri - triStart;
        }

        #region Neighbor Checks
        private void CheckWestNeighbor(int x, int z, HeightPoint currentPoint, 
            ref int currentShadowVertex, ref int currentShadowTri)
        {
            if (x > 0)
            {
                var westPoint = heightMap[z * ChunkData.SIZE + x - 1];
                if (westPoint.height < currentPoint.height)
                {
                    AddWestShadowFace(x, z, currentPoint.height, westPoint.height, 
                        ref currentShadowVertex, ref currentShadowTri);
                }
            }
        }

        private void CheckEastNeighbor(int x, int z, HeightPoint currentPoint, 
            ref int currentShadowVertex, ref int currentShadowTri)
        {
            if (x < ChunkData.SIZE - 1)
            {
                var eastPoint = heightMap[z * ChunkData.SIZE + x + 1];
                if (eastPoint.height < currentPoint.height)
                {
                    AddEastShadowFace(x, z, currentPoint.height, eastPoint.height, 
                        ref currentShadowVertex, ref currentShadowTri);
                }
            }
        }

        private void CheckNorthNeighbor(int x, int z, HeightPoint currentPoint, 
            ref int currentShadowVertex, ref int currentShadowTri)
        {
            if (z > 0)
            {
                var northPoint = heightMap[(z - 1) * ChunkData.SIZE + x];
                if (northPoint.height < currentPoint.height)
                {
                    AddNorthShadowFace(x, z, currentPoint.height, northPoint.height, 
                        ref currentShadowVertex, ref currentShadowTri);
                }
            }
        }

        private void CheckSouthNeighbor(int x, int z, HeightPoint currentPoint, 
            ref int currentShadowVertex, ref int currentShadowTri)
        {
            if (z < ChunkData.SIZE - 1)
            {
                var southPoint = heightMap[(z + 1) * ChunkData.SIZE + x];
                if (southPoint.height < currentPoint.height)
                {
                    AddSouthShadowFace(x, z, currentPoint.height, southPoint.height, 
                        ref currentShadowVertex, ref currentShadowTri);
                }
            }
        }
        #endregion

        #region Shadow Face Addition
        private void AddWestShadowFace(int x, int z, float currentHeight, float neighborHeight, 
            ref int currentShadowVertex, ref int currentShadowTri)
        {
            // Heights are already in world space (0-255)
            shadowVertices[currentShadowVertex + 0] = new float3(x, currentHeight, z);
            shadowVertices[currentShadowVertex + 1] = new float3(x, neighborHeight, z);
            shadowVertices[currentShadowVertex + 2] = new float3(x, neighborHeight, z + 1);
            shadowVertices[currentShadowVertex + 3] = new float3(x, currentHeight, z + 1);

            // Add normals (pointing west)
            float3 normal = new float3(-1, 0, 0);
            shadowNormals[currentShadowVertex + 0] = normal;
            shadowNormals[currentShadowVertex + 1] = normal;
            shadowNormals[currentShadowVertex + 2] = normal;
            shadowNormals[currentShadowVertex + 3] = normal;

            AddShadowQuadTriangles(ref currentShadowTri, currentShadowVertex);
            currentShadowVertex += 4;
        }

        private void AddEastShadowFace(int x, int z, float currentHeight, float neighborHeight, 
            ref int currentShadowVertex, ref int currentShadowTri)
        {
            shadowVertices[currentShadowVertex + 0] = new float3(x + 1, currentHeight, z + 1);
            shadowVertices[currentShadowVertex + 1] = new float3(x + 1, neighborHeight, z + 1);
            shadowVertices[currentShadowVertex + 2] = new float3(x + 1, neighborHeight, z);
            shadowVertices[currentShadowVertex + 3] = new float3(x + 1, currentHeight, z);

            // Add normals (pointing east)
            float3 normal = new float3(1, 0, 0);
            shadowNormals[currentShadowVertex + 0] = normal;
            shadowNormals[currentShadowVertex + 1] = normal;
            shadowNormals[currentShadowVertex + 2] = normal;
            shadowNormals[currentShadowVertex + 3] = normal;

            AddShadowQuadTriangles(ref currentShadowTri, currentShadowVertex);
            currentShadowVertex += 4;
        }

        private void AddNorthShadowFace(int x, int z, float currentHeight, float neighborHeight, 
            ref int currentShadowVertex, ref int currentShadowTri)
        {
            shadowVertices[currentShadowVertex + 0] = new float3(x + 1, currentHeight, z);
            shadowVertices[currentShadowVertex + 1] = new float3(x + 1, neighborHeight, z);
            shadowVertices[currentShadowVertex + 2] = new float3(x, neighborHeight, z);
            shadowVertices[currentShadowVertex + 3] = new float3(x, currentHeight, z);

            // Add normals (pointing north)
            float3 normal = new float3(0, 0, 1);
            shadowNormals[currentShadowVertex + 0] = normal;
            shadowNormals[currentShadowVertex + 1] = normal;
            shadowNormals[currentShadowVertex + 2] = normal;
            shadowNormals[currentShadowVertex + 3] = normal;

            AddShadowQuadTriangles(ref currentShadowTri, currentShadowVertex);
            currentShadowVertex += 4;
        }

        private void AddSouthShadowFace(int x, int z, float currentHeight, float neighborHeight, 
            ref int currentShadowVertex, ref int currentShadowTri)
        {
            shadowVertices[currentShadowVertex + 0] = new float3(x, currentHeight, z + 1);
            shadowVertices[currentShadowVertex + 1] = new float3(x, neighborHeight, z + 1);
            shadowVertices[currentShadowVertex + 2] = new float3(x + 1, neighborHeight, z + 1);
            shadowVertices[currentShadowVertex + 3] = new float3(x + 1, currentHeight, z + 1);

            // Add normals (pointing south)
            float3 normal = new float3(0, 0, -1);
            shadowNormals[currentShadowVertex + 0] = normal;
            shadowNormals[currentShadowVertex + 1] = normal;
            shadowNormals[currentShadowVertex + 2] = normal;
            shadowNormals[currentShadowVertex + 3] = normal;

            AddShadowQuadTriangles(ref currentShadowTri, currentShadowVertex);
            currentShadowVertex += 4;
        }
        #endregion
    }
}