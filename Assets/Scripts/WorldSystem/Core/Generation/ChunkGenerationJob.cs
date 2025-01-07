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
}