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
        private int blendDistance;

        public BiomeGenerator(NoiseSettings biomeNoise, NativeArray<BiomeSettings> biomes, int blendDistance)
        {
            this.biomeNoise = biomeNoise;
            this.biomes = biomes;
            this.blendDistance = blendDistance;
        }

        [BurstCompile]
        public void GetBiomeWeights(float2 position, NativeArray<float> weights)
        {
            float temperature = NoiseUtility.Sample2D(position, biomeNoise);
            float humidity = NoiseUtility.Sample2D(position + 1000f, biomeNoise);

            float totalWeight = 0f;
            for (int i = 0; i < biomes.Length; i++)
            {
                float tempDiff = math.abs(temperature - biomes[i].Temperature);
                float humidDiff = math.abs(humidity - biomes[i].Humidity);
                
                float weight = 1f / (1f + tempDiff + humidDiff);
                weights[i] = weight;
                totalWeight += weight;
            }

            // Normalize weights
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] /= totalWeight;
            }
        }
    }
} 