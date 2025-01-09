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
            float continentalness = NoiseUtility.Sample2D(position + 2000f, 
                new NoiseSettings 
                { 
                    Scale = biomeNoise.Scale * 3,
                    Amplitude = biomeNoise.Amplitude,
                    Frequency = biomeNoise.Frequency * 0.3f,
                    Octaves = biomeNoise.Octaves,
                    Persistence = biomeNoise.Persistence,
                    Lacunarity = biomeNoise.Lacunarity,
                    Seed = biomeNoise.Seed + 1000
                });

            float totalWeight = 0f;
            for (int i = 0; i < biomes.Length; i++)
            {
                float tempDiff = math.abs(temperature - biomes[i].Temperature);
                float humidDiff = math.abs(humidity - biomes[i].Humidity);
                float contDiff = math.abs(continentalness - biomes[i].Continentalness);
                
                // Calculate distance with higher emphasis on differences
                float distance = tempDiff * tempDiff + humidDiff * humidDiff + contDiff * contDiff;
                
                // More aggressive falloff using exponential
                float weight = math.exp(-distance * 8f); // Increased from default of ~1
                weights[i] = weight;
                totalWeight += weight;
            }

            // Normalize weights
            if (totalWeight > 0f)
            {
                for (int i = 0; i < weights.Length; i++)
                {
                    weights[i] /= totalWeight;
                    
                    // Apply additional power function to make high weights even higher
                    weights[i] = math.pow(weights[i], 8f);
                }
                
                // Renormalize after power function
                totalWeight = 0f;
                for (int i = 0; i < weights.Length; i++)
                    totalWeight += weights[i];
                    
                for (int i = 0; i < weights.Length; i++)
                    weights[i] /= totalWeight;
            }
        }
    }
} 