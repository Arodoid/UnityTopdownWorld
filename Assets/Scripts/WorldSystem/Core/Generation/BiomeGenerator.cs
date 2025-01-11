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
        [ReadOnly] private float falloffMultiplier;

        public BiomeGenerator(NoiseSettings biomeNoise, NativeArray<BiomeSettings> biomes, float falloffMultiplier)
        {
            this.biomeNoise = biomeNoise;
            this.biomes = biomes;
            this.falloffMultiplier = falloffMultiplier;
        }

        [BurstCompile]
        public void GetBiomeWeights(float2 position, NativeArray<float> weights)
        {
            // Convert from -1,1 to 0,1 range
            float temperature = (NoiseUtility.Sample2D(position, biomeNoise) + 1f) * 0.5f;
            float humidity = (NoiseUtility.Sample2D(position + 1000f, biomeNoise) + 1f) * 0.5f;
            float continentalness = (NoiseUtility.Sample2D(position + 2000f, biomeNoise) + 1f) * 0.5f;

            // First pass: calculate raw weights with better numerical stability
            float maxWeight = float.MinValue;
            var rawWeights = new NativeArray<float>(biomes.Length, Allocator.Temp);
            
            for (int i = 0; i < biomes.Length; i++)
            {
                float distanceSquared = 
                    math.pow(temperature - biomes[i].Temperature, 2) +
                    math.pow(humidity - biomes[i].Humidity, 2) +
                    math.pow(continentalness - biomes[i].Continentalness, 2);
                    
                // Store the negative distance (higher is better) to prevent tiny numbers
                rawWeights[i] = -distanceSquared * falloffMultiplier * 16f;
                maxWeight = math.max(maxWeight, rawWeights[i]);
            }

            // Second pass: normalize relative to max weight to prevent numerical underflow
            float totalWeight = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] = math.exp(rawWeights[i] - maxWeight);
                totalWeight += weights[i];
            }

            rawWeights.Dispose();

            // Ensure minimum weight and normalize
            const float MIN_WEIGHT = 0.001f;
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] = math.max(weights[i] / totalWeight, MIN_WEIGHT);
            }

            // Final normalization to ensure weights sum to 1
            totalWeight = 0f;
            for (int i = 0; i < weights.Length; i++)
                totalWeight += weights[i];
            
            for (int i = 0; i < weights.Length; i++)
                weights[i] /= totalWeight;
        }
    }
} 