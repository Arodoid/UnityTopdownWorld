using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using WorldSystem.Data;


namespace WorldSystem.Generation.Jobs
{
    [BurstCompile]
    public struct BiomeRegionJob : IJobParallelFor
    {
        [ReadOnly] public int2 chunkPosition;
        [ReadOnly] public int seed;
        [ReadOnly] public NativeArray<float> continentalness;
        public NativeArray<BiomeData> biomeData;

        public void Execute(int index)
        {
            int x = index % ChunkData.SIZE;
            int z = index / ChunkData.SIZE;

            float2 worldPos = new float2(
                chunkPosition.x * ChunkData.SIZE + x,
                chunkPosition.y * ChunkData.SIZE + z
            );

            biomeData[index] = BiomeData.Sample(worldPos, seed);
            // Modify biome data based on continentalness
            var data = biomeData[index];
            data.continentalness = continentalness[index];
            biomeData[index] = data;
        }
    }
} 