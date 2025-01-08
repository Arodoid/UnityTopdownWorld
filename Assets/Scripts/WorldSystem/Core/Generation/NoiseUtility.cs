using Unity.Mathematics;
using UnityEngine;

namespace WorldSystem.Generation
{
    public static class NoiseUtility
    {
        public static float Hash(float2 p, float seed)
        {
            p = new float2(
                math.dot(p, new float2(127.1f + seed, 311.7f)),
                math.dot(p, new float2(269.5f, 183.3f + seed))
            );
            return -1f + 2f * math.frac(math.sin(p.x + p.y * 17.17f) * 43758.5453123f);
        }

        public static float Noise(float2 p, float seed)
        {
            const float K1 = 0.366025404f; // (sqrt(3)-1)/2
            const float K2 = 0.211324865f; // (3-sqrt(3))/6
            
            float2 i = math.floor(p + (p.x + p.y) * K1);
            float2 a = p - i + (i.x + i.y) * K2;
            float2 o = a.x > a.y ? new float2(1f, 0f) : new float2(0f, 1f);
            float2 b = a - o + K2;
            float2 c = a - 1f + 2f * K2;
            
            float3 h = math.max(0.5f - new float3(
                math.dot(a, a),
                math.dot(b, b),
                math.dot(c, c)
            ), 0f);
            
            float3 n = h * h * h * h * new float3(
                math.dot(a, Hash2D(i, seed)),
                math.dot(b, Hash2D(i + o, seed)),
                math.dot(c, Hash2D(i + 1f, seed))
            );
            
            return math.dot(n, new float3(70f, 70f, 70f));
        }

        private static float2 Hash2D(float2 p, float seed)
        {
            p = new float2(
                math.dot(p, new float2(127.1f + seed, 311.7f)),
                math.dot(p, new float2(269.5f, 183.3f + seed))
            );
            return -1f + 2f * math.frac(math.sin(p) * 43758.5453123f);
        }

        public static float FBM(float2 p, float seed, int octaves = 5)
        {
            float value = 0f;
            float amplitude = 0.5f;
            float frequency = 1f;
            
            for (int i = 0; i < octaves; i++)
            {
                value += amplitude * Noise(p * frequency, seed);
                frequency *= 2f;
                amplitude *= 0.5f;
            }
            
            return value;
        }

        // Helper to convert to shader-compatible string
        public static string GetNoiseShaderFunctions()
        {
            return @"
            float2 hash2(float2 p, float seed)
            {
                p = float2(dot(p,float2(127.1 + seed, 311.7)), dot(p,float2(269.5, 183.3 + seed)));
                return -1.0 + 2.0 * frac(sin(p) * 43758.5453123);
            }

            float noise(float2 p, float seed)
            {
                const float K1 = 0.366025404;
                const float K2 = 0.211324865;
                
                float2 i = floor(p + (p.x + p.y) * K1);
                float2 a = p - i + (i.x + i.y) * K2;
                float2 o = (a.x > a.y) ? float2(1.0, 0.0) : float2(0.0, 1.0);
                float2 b = a - o + K2;
                float2 c = a - 1.0 + 2.0 * K2;
                
                float3 h = max(0.5 - float3(dot(a,a), dot(b,b), dot(c,c)), 0.0);
                float3 n = h * h * h * h * float3(
                    dot(a, hash2(i, seed)),
                    dot(b, hash2(i + o, seed)),
                    dot(c, hash2(i + 1.0, seed))
                );
                
                return dot(n, float3(70.0, 70.0, 70.0));
            }

            float fbm(float2 p, float seed)
            {
                float f = 0.0;
                float w = 0.5;
                for (int i = 0; i < 5; i++)
                {
                    f += w * noise(p, seed);
                    p *= 2.0;
                    w *= 0.5;
                }
                return f;
            }";
        }
    }
} 