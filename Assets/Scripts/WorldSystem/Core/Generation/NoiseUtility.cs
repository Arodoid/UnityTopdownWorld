using Unity.Mathematics;
using Unity.Burst;
using WorldSystem.Generation;

[BurstCompile]
public static class NoiseUtility
{
    [BurstCompile]
    public static float Sample3D(in float3 position, in NoiseSettings settings)
    {
        float amplitude = settings.Amplitude;
        float frequency = settings.Frequency;
        float noiseHeight = 0;
        float amplitudeSum = 0;

        float3 scaledPos = position / settings.Scale;

        for (int i = 0; i < settings.Octaves; i++)
        {
            float3 pos = scaledPos * frequency;
            float noiseValue = noise.snoise(pos + settings.Seed);
            
            noiseHeight += noiseValue * amplitude;
            amplitudeSum += amplitude;
            
            amplitude *= settings.Persistence;
            frequency *= settings.Lacunarity;
        }

        return math.clamp(noiseHeight / amplitudeSum * settings.Amplitude, -1f, 1f);
    }

    [BurstCompile]
    public static float Sample2D(in float2 position, in NoiseSettings settings)
    {
        float2 scaledPos = position / settings.Scale;
        float3 pos = new float3(scaledPos.x, 0, scaledPos.y);
        return Sample3D(in pos, in settings);
    }
}