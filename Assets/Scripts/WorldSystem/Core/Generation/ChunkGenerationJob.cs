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
        [ReadOnly] public int3 position;
        [ReadOnly] public int seed;
        
        // Output
        [NativeDisableParallelForRestriction]
        public NativeArray<byte> blocks;
        [NativeDisableParallelForRestriction]
        public NativeArray<HeightPoint> heightMap;
        public bool isFullyOpaque;

        // Constants
        private const float NOISE_SCALE = 0.03f;
        private const float HEIGHT_SCALE = 8f;
        private const float BASE_HEIGHT = 64f;

        private float GetNoise(float2 pos)
        {
            float2 worldPos = new float2(
                (position.x * ChunkData.SIZE + pos.x) * NOISE_SCALE,
                (position.z * ChunkData.SIZE + pos.y) * NOISE_SCALE
            );

            return noise.snoise(worldPos) * HEIGHT_SCALE + BASE_HEIGHT;
        }

        private BlockType GetBlockType(int worldHeight, int worldY)
        {
            if (worldY > worldHeight) return BlockType.Air;
            if (worldY == worldHeight) return BlockType.Grass;
            if (worldY > worldHeight - 3) return BlockType.Dirt;
            return BlockType.Stone;
        }

        public void Execute(int index)
        {
            int x = index % ChunkData.SIZE;
            int z = index / ChunkData.SIZE;

            // Calculate absolute world height for this column
            float heightNoise = GetNoise(new float2(x, z));
            int worldHeight = (int)math.clamp(heightNoise, 0, 255); // Use full world height range

            // Store heightmap data
            int mapIndex = z * ChunkData.SIZE + x;
            heightMap[mapIndex] = new HeightPoint 
            { 
                height = (byte)worldHeight,
                blockType = (byte)GetBlockType(worldHeight, worldHeight)
            };

            // Fill chunk data
            isFullyOpaque = true;
            int chunkBaseY = position.y * ChunkData.SIZE;
            
            for (int y = 0; y < ChunkData.SIZE; y++)
            {
                int worldY = chunkBaseY + y;
                int blockIndex = (y * ChunkData.SIZE * ChunkData.SIZE) + (z * ChunkData.SIZE) + x;
                BlockType type = GetBlockType(worldHeight, worldY);
                blocks[blockIndex] = (byte)type;
                isFullyOpaque &= BlockColors.Definitions[(int)type].isOpaque;
            }
        }
    }
}