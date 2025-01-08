using Unity.Mathematics;
using Unity.Burst;

namespace WorldSystem.Generation
{
    [BurstCompile]
    public static class WorldNoise
    {
        public static float Sample(float2 pos, float scale, int seed)
        {
            // Returns noise in range [0,1]
            return NoiseUtility.FBM(pos * scale, seed) * 0.5f + 0.5f;
        }

        public static float SampleContinental(float2 pos, float scale, int seed)
        {
            float noiseValue = Sample(pos, scale, seed);
            // Reshape noise to create more distinct continents
            // Power function creates sharper transitions between ocean and land
            return math.pow(noiseValue, 2.5f);
        }

        public static float SampleErosion(float2 pos, float scale, int seed)
        {
            float noiseValue = Sample(pos, scale, seed);
            float detail = Sample(pos, scale * 4, seed + 1000);
            return math.lerp(noiseValue, detail, noiseValue * 0.5f);
        }
    }
} 