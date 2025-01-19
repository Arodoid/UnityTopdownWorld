using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using WorldSystem.Data;

namespace WorldSystem.Jobs
{
    [BurstCompile]
    public struct HeightMapGenerationJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<byte> blocks;
        [WriteOnly] public NativeArray<HeightPoint> heightMap;
        [ReadOnly] public int maxYLevel;
        
        public void Execute(int index)
        {
            int x = index % ChunkData.SIZE;
            int z = index / ChunkData.SIZE;
            
            // Scan from maxYLevel down (instead of ChunkData.HEIGHT)
            for (int y = maxYLevel - 1; y >= 0; y--)
            {
                int blockIndex = (y * ChunkData.SIZE * ChunkData.SIZE) + (z * ChunkData.SIZE) + x;
                byte blockType = blocks[blockIndex];
                
                if (blockType != 0) // Found highest non-air block
                {
                    heightMap[z * ChunkData.SIZE + x] = new HeightPoint
                    {
                        height = (byte)y,
                        blockType = blockType
                    };
                    return;
                }
            }
            
            // Chunk is all air up to maxYLevel
            heightMap[z * ChunkData.SIZE + x] = new HeightPoint
            {
                height = 0,
                blockType = 0
            };
        }
    }
} 