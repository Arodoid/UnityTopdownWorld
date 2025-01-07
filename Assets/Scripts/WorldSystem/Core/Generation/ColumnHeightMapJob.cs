using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using WorldSystem.Data;

namespace WorldSystem.Jobs
{
    [BurstCompile]
    public struct ColumnHeightMapJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<NativeArray<byte>> chunks;
        [ReadOnly] public NativeArray<int3> chunkPositions;
        [WriteOnly] public NativeArray<HeightPoint> heightMap;
        public int totalColumnHeight;
        public int topChunkY;

        public void Execute(int index)
        {
            int x = index % ChunkData.SIZE;
            int z = index / ChunkData.SIZE;
            
            // Scan from top to bottom through all chunks
            for (int worldY = totalColumnHeight - 1; worldY >= 0; worldY--)
            {
                // Calculate which chunk this Y position is in
                int chunkY = worldY / ChunkData.SIZE;
                int localY = worldY % ChunkData.SIZE;
                int chunkIndex = topChunkY - chunkPositions[0].y - chunkY;
                
                if (chunkIndex < 0 || chunkIndex >= chunks.Length) continue;

                var chunk = chunks[chunkIndex];
                int blockIndex = (localY * ChunkData.SIZE * ChunkData.SIZE) + (z * ChunkData.SIZE) + x;
                BlockType type = (BlockType)chunk[blockIndex];
                
                if (type != BlockType.Air)
                {
                    // Found highest non-air block in this column
                    heightMap[z * ChunkData.SIZE + x] = new HeightPoint
                    {
                        height = (byte)worldY,
                        blockType = (byte)type
                    };
                    return;
                }
            }
            
            // If we get here, entire column is air.
            heightMap[z * ChunkData.SIZE + x] = new HeightPoint
            {
                height = 0,
                blockType = (byte)BlockType.Air
            };
        }
    }
} 