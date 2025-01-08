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
        public float continentalness;
        public float weirdness;
        public float erosion;
        
        public float GetMountainWeight()
        {
            float heightInfluence = math.smoothstep(0.75f, 0.85f, continentalness);
            float tempInfluence = math.smoothstep(0.3f, 0f, temperature);
            float erosionInfluence = math.smoothstep(1f, 0f, erosion);
            
            return heightInfluence * tempInfluence * erosionInfluence;
        }

        public float GetPlainsWeight()
        {
            return math.smoothstep(0.4f, 0.6f, continentalness) 
                 * math.smoothstep(0.3f, 0.7f, temperature);
        }

        public float GetForestWeight()
        {
            return math.smoothstep(0.5f, 0.7f, moisture) 
                 * math.smoothstep(0.3f, 0.6f, temperature);
        }

        public static BiomeData Sample(float2 worldPos, int seed)
        {
            const float CONTINENT_SCALE = 0.002f;
            const float TEMPERATURE_SCALE = 0.003f;
            const float MOISTURE_SCALE = 0.0025f;
            const float WEIRDNESS_SCALE = 0.004f;
            const float EROSION_SCALE = 0.003f;

            return new BiomeData
            {
                continentalness = WorldNoise.Sample(worldPos, CONTINENT_SCALE, seed),
                temperature = WorldNoise.Sample(worldPos, TEMPERATURE_SCALE, seed + 1000),
                moisture = WorldNoise.Sample(worldPos, MOISTURE_SCALE, seed + 2000),
                weirdness = WorldNoise.Sample(worldPos, WEIRDNESS_SCALE, seed + 3000),
                erosion = WorldNoise.SampleErosion(worldPos, EROSION_SCALE, seed + 4000)
            };
        }
    }
} 