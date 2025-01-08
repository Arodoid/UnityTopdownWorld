using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using WorldSystem.Data;

namespace WorldSystem.Generation.Jobs
{
    [BurstCompile]
    public struct ContinentGenerationJob : IJobParallelFor
    {
        [ReadOnly] public int2 chunkPosition;
        [ReadOnly] public int seed;
        public NativeArray<float> continentalness;

        private const float CONTINENT_SCALE = 0.001f;
        private const float OCEAN_THRESHOLD = 0.4f;

        public void Execute(int index)
        {
            int x = index % ChunkData.SIZE;
            int z = index / ChunkData.SIZE;

            float2 worldPos = new float2(
                chunkPosition.x * ChunkData.SIZE + x,
                chunkPosition.y * ChunkData.SIZE + z
            );

            float value = WorldNoise.SampleContinental(worldPos, CONTINENT_SCALE, seed);
            continentalness[index] = value;
        }
    }
} 