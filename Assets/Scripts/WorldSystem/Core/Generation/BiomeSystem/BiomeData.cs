using Unity.Mathematics;

namespace WorldSystem.Generation
{
    public enum BiomeType
    {
        Ocean,
        Plains,
        Forest,
        Desert,
        Mountains,
        Tundra
    }

    public struct BiomeData
    {
        public float temperature;
        public float moisture;
        public float continentalness; // How "inland" we are (0 = ocean, 1 = deep inland)
        public float weirdness; // Random variation to break up patterns
        public float erosion;  // New field for erosion
        
        public static BiomeData Sample(float2 worldPos, int seed)
        {
            const float CONTINENT_SCALE = 0.001f;
            const float TEMPERATURE_SCALE = 0.002f;
            const float MOISTURE_SCALE = 0.0015f;
            const float WEIRDNESS_SCALE = 0.003f;
            const float EROSION_SCALE = 0.002f;  // New scale for erosion

            return new BiomeData
            {
                continentalness = WorldNoise.Sample(worldPos, CONTINENT_SCALE, seed),
                temperature = WorldNoise.Sample(worldPos, TEMPERATURE_SCALE, seed + 1000),
                moisture = WorldNoise.Sample(worldPos, MOISTURE_SCALE, seed + 2000),
                weirdness = WorldNoise.Sample(worldPos, WEIRDNESS_SCALE, seed + 3000),
                erosion = WorldNoise.SampleErosion(worldPos, EROSION_SCALE, seed + 4000)  // New erosion sampling
            };
        }
    }
} 