using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace VoxelGame.WorldSystem.Jobs
{
    [BurstCompile]
    public struct MeshGenerationJob : IJob
    {
        public ChunkJobData chunkData;
        
        // Output mesh data
        public NativeList<float3> vertices;
        public NativeList<int> triangles;
        public NativeList<float2> uvs;
        public NativeList<float4> colors;

        // Make merged public and remove allocation from Execute
        public NativeArray<bool> merged;

        public void Execute()
        {
            switch (chunkData.lodLevel)
            {
                case MeshLODLevel.High:
                    GenerateHighLODMesh();
                    break;
                case MeshLODLevel.Ultra:
                    GenerateUltraLODMesh();
                    break;
            }
        }

        private void GenerateHighLODMesh()
        {
            // Process each layer from top down
            for (int y = chunkData.maxYLevel - 1; y >= 0; y--)
            {
                ProcessLayer(y);
                // Clear the merged array
                for (int i = 0; i < merged.Length; i++)
                {
                    merged[i] = false;
                }
            }
        }

        private void GenerateUltraLODMesh()
        {
            // Generate single quad for entire chunk
            int maxHeight = 0;
            float4 dominantColor = GetDominantColor();

            for (int y = chunkData.maxYLevel - 1; y >= 0; y--)
            for (int x = 0; x < chunkData.chunkSize; x++)
            for (int z = 0; z < chunkData.chunkSize; z++)
            {
                BlockData block = GetBlock(x, y, z);
                if (block.blockType != 0)
                {
                    maxHeight = math.max(maxHeight, y + 1);
                }
            }

            if (maxHeight > 0)
            {
                AddSimpleQuad(new int3(0, maxHeight, 0), chunkData.chunkSize, dominantColor);
            }
        }

        private float4 GetDominantColor()
        {
            // Simple average color calculation
            float4 sum = float4.zero;
            int count = 0;

            for (int y = chunkData.maxYLevel - 1; y >= 0; y--)
            for (int x = 0; x < chunkData.chunkSize; x++)
            for (int z = 0; z < chunkData.chunkSize; z++)
            {
                BlockData block = GetBlock(x, y, z);
                if (block.blockType != 0)
                {
                    sum += block.color;
                    count++;
                }
            }

            return count > 0 ? sum / count : new float4(1, 1, 1, 1);
        }

        private int GetMaxHeight(int startX, int startZ, int size)
        {
            int maxHeight = 0;
            for (int x = startX; x < math.min(startX + size, chunkData.chunkSize); x++)
            for (int z = startZ; z < math.min(startZ + size, chunkData.chunkSize); z++)
            for (int y = chunkData.maxYLevel - 1; y >= 0; y--)
            {
                BlockData block = GetBlock(x, y, z);
                if (block.blockType != 0)
                {
                    maxHeight = math.max(maxHeight, y + 1);
                }
            }
            return maxHeight;
        }

        private void AddSimpleQuad(int3 start, int size, float4 color)
        {
            int vertexStart = vertices.Length;

            // Add vertices for the quad
            float3 pos = new float3(start.x, start.y, start.z);
            vertices.Add(pos);
            vertices.Add(pos + new float3(size, 0, 0));
            vertices.Add(pos + new float3(size, 0, size));
            vertices.Add(pos + new float3(0, 0, size));

            // Add colors
            for (int i = 0; i < 4; i++)
            {
                colors.Add(color);
            }

            // Add UVs
            float2 dummyUV = float2.zero;
            for (int i = 0; i < 4; i++)
            {
                uvs.Add(dummyUV);
            }

            // Add triangles
            triangles.Add(vertexStart);
            triangles.Add(vertexStart + 2);
            triangles.Add(vertexStart + 1);
            triangles.Add(vertexStart);
            triangles.Add(vertexStart + 3);
            triangles.Add(vertexStart + 2);
        }

        private void ProcessLayer(int y)
        {
            // Scan each cell in the layer
            for (int x = 0; x < chunkData.chunkSize; x++)
            for (int z = 0; z < chunkData.chunkSize; z++)
            {
                int index = (x * chunkData.chunkSize) + z;
                if (merged[index]) continue; // Skip if already part of a merged rectangle

                BlockData block = GetBlock(x, y, z);
                if (block.blockType == 0 || !ShouldRenderTop(x, y, z)) continue;

                // Try to merge a rectangle starting at this point
                MergeRect(x, y, z, block);
            }
        }

        private void MergeRect(int startX, int y, int startZ, BlockData block)
        {
            int width = 1;
            int depth = 1;

            // Expand in X direction
            while (startX + width < chunkData.chunkSize)
            {
                if (!CanMergeX(startX + width, y, startZ, depth, block)) break;
                width++;
            }

            // Expand in Z direction
            while (startZ + depth < chunkData.chunkSize)
            {
                if (!CanMergeZ(startX, y, startZ + depth, width, block)) break;
                depth++;
            }

            // Mark all blocks in the rectangle as merged
            for (int x = 0; x < width; x++)
            for (int z = 0; z < depth; z++)
            {
                merged[(startX + x) * chunkData.chunkSize + (startZ + z)] = true;
            }

            // Generate mesh data for this rectangle
            AddRect(new int3(startX, y, startZ), new int2(width, depth), block);
        }

        private bool CanMergeX(int x, int y, int z, int depth, BlockData targetBlock)
        {
            for (int dz = 0; dz < depth; dz++)
            {
                int index = x * chunkData.chunkSize + (z + dz);
                if (merged[index]) return false;

                BlockData block = GetBlock(x, y, z + dz);
                if (!BlocksMatch(block, targetBlock) || !ShouldRenderTop(x, y, z + dz))
                    return false;
            }
            return true;
        }

        private bool CanMergeZ(int x, int y, int z, int width, BlockData targetBlock)
        {
            for (int dx = 0; dx < width; dx++)
            {
                int index = (x + dx) * chunkData.chunkSize + z;
                if (merged[index]) return false;

                BlockData block = GetBlock(x + dx, y, z);
                if (!BlocksMatch(block, targetBlock) || !ShouldRenderTop(x + dx, y, z))
                    return false;
            }
            return true;
        }

        private bool BlocksMatch(BlockData a, BlockData b)
        {
            return a.blockType == b.blockType && 
                   math.all(a.color == b.color) && 
                   math.all(a.uvStart == b.uvStart);
        }

        private bool ShouldRenderTop(int x, int y, int z)
        {
            // Don't render if block above is solid
            if (y + 1 < chunkData.maxYLevel)
            {
                BlockData blockAbove = GetBlock(x, y + 1, z);
                if (blockAbove.blockType != 0 && blockAbove.isOpaque)
                    return false;
            }
            return true;
        }

        private BlockData GetBlock(int x, int y, int z)
        {
            int index = (y * chunkData.chunkSize * chunkData.chunkSize) + 
                       (x * chunkData.chunkSize) + z;
            return chunkData.blocks[index];
        }

        private void AddRect(int3 start, int2 size, BlockData block)
        {
            int vertexStart = vertices.Length;

            // Add vertices for the rectangle
            float3 pos = new float3(start.x, start.y + 1, start.z); // +1 for top face
            vertices.Add(pos);
            vertices.Add(pos + new float3(size.x, 0, 0));
            vertices.Add(pos + new float3(size.x, 0, size.y));
            vertices.Add(pos + new float3(0, 0, size.y));

            // Add colors (use the block's color)
            float4 color = block.color;
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);

            // Add dummy UVs (required but not used)
            float2 dummyUV = float2.zero;
            uvs.Add(dummyUV);
            uvs.Add(dummyUV);
            uvs.Add(dummyUV);
            uvs.Add(dummyUV);

            // Add triangles (counter-clockwise winding)
            triangles.Add(vertexStart);
            triangles.Add(vertexStart + 2);
            triangles.Add(vertexStart + 1);
            triangles.Add(vertexStart);
            triangles.Add(vertexStart + 3);
            triangles.Add(vertexStart + 2);
        }
    }
} 