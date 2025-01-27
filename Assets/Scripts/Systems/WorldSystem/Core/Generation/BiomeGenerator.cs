using Unity.Mathematics;
using Unity.Collections;
using Unity.Burst;
using WorldSystem.Generation;
using WorldSystem.Data;

namespace WorldSystem.Generation
{
    [BurstCompile]
    public struct BiomeGenerator
    {
        [ReadOnly] public NativeArray<BiomeSettings> Biomes;
        
        // Pre-calculated noise values for the chunk
        [ReadOnly] public NativeArray<float> TemperatureMap;
        [ReadOnly] public NativeArray<float> HumidityMap;
        [ReadOnly] public NativeArray<float> ContinentalnessMap; // Same as height noise
        
        public float WaterLevel;
        
        public BiomeGenerator(NativeArray<BiomeSettings> biomes, float waterLevel)
        {
            this.Biomes = biomes;
            this.TemperatureMap = default;
            this.HumidityMap = default;
            this.ContinentalnessMap = default;
            this.WaterLevel = waterLevel;
        }

        [BurstCompile]
        public BlockType GetSurfaceBlock(int x, int z, float height)
        {
            int index = x + z * Data.ChunkData.SIZE;
            
            float temperature = TemperatureMap[index];
            float humidity = HumidityMap[index];
            float continentalness = ContinentalnessMap[index];
            
            // Find best matching biome using simple distance
            int bestBiome = 0;
            float bestMatch = float.MaxValue;
            
            for (int i = 0; i < Biomes.Length; i++)
            {
                var biome = Biomes[i];
                
                float tempDiff = temperature - biome.PreferredTemperature;
                float humidDiff = humidity - biome.PreferredHumidity;
                float contDiff = continentalness - biome.PreferredContinentalness;
                
                // Simple squared distance
                float match = tempDiff * tempDiff + 
                             humidDiff * humidDiff + 
                             contDiff * contDiff;
                
                if (match < bestMatch)
                {
                    bestMatch = match;
                    bestBiome = i;
                }
            }
            
            var selectedBiome = Biomes[bestBiome];
            return height < WaterLevel 
                ? selectedBiome.UnderwaterBlock 
                : selectedBiome.TopBlock;
        }
    }
}