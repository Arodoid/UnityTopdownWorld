using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using WorldSystem.Data;
using UnityEngine;

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
        public bool isFullyOpaque;

        // Simplified constants
        private const float NOISE_SCALE = 0.03f;
        private const int TERRAIN_HEIGHT = 64; // Fixed base terrain height

        public void Execute(int index)
        {
            int x = index % ChunkData.SIZE;
            int z = index / ChunkData.SIZE;

            // Simple height calculation
            float2 worldPos = new float2(
                (position.x * ChunkData.SIZE + x) * NOISE_SCALE,
                (position.z * ChunkData.SIZE + z) * NOISE_SCALE
            );
            
            int height = TERRAIN_HEIGHT + (int)(noise.snoise(worldPos) * 8); // +/- 8 blocks variation

            // Fill Chunk
            for (int y = 0; y < ChunkData.HEIGHT; y++)
            {
                int blockIndex = (y * ChunkData.SIZE * ChunkData.SIZE) + (z * ChunkData.SIZE) + x;
                BlockType type;

                if (y > height)
                    type = BlockType.Air;
                else if (y == height)
                    type = BlockType.Grass;
                else if (y >= height - 2)
                    type = BlockType.Dirt;
                else
                    type = BlockType.Stone;

                blocks[blockIndex] = (byte)type;
                isFullyOpaque &= type != BlockType.Air;
            }
        }
    }
}