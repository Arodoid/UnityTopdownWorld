using Unity.Mathematics;
using Unity.Collections;
using Unity.Burst;
using WorldSystem.Generation;

namespace WorldSystem.Generation
{
    [BurstCompile]
    public struct BiomeGenerator
    {
        [ReadOnly] private NoiseSettings biomeNoise;
        [ReadOnly] private NativeArray<BiomeSettings> biomes;

        public BiomeGenerator(NoiseSettings biomeNoise, NativeArray<BiomeSettings> biomes, int blendDistance)
        {
            this.biomeNoise = biomeNoise;
            this.biomes = biomes;
        }

        [BurstCompile]
        public void GetBiomeWeights(float2 position, NativeArray<float> weights)
        {
            float temperature = NoiseUtility.Sample2D(position, biomeNoise);
            float humidity = NoiseUtility.Sample2D(position + 1000f, biomeNoise);
            float continentalness = NoiseUtility.Sample2D(position + 2000f, biomeNoise);

            float totalWeight = 0f;
            for (int i = 0; i < biomes.Length; i++)
            {
                float distance = 
                    math.abs(temperature - biomes[i].Temperature) +
                    math.abs(humidity - biomes[i].Humidity) +
                    math.abs(continentalness - biomes[i].Continentalness);
                    
                weights[i] = math.exp(-distance);
                totalWeight += weights[i];
            }

            if (totalWeight > 0f)
            {
                for (int i = 0; i < weights.Length; i++)
                    weights[i] /= totalWeight;
            }
        }
    }
} 