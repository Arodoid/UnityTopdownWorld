using UnityEngine;

namespace VoxelGame.WorldSystem.Generation.Core
{
    public class NoiseGenerator
    {
        private readonly int seed;
        private readonly System.Random random;
        private readonly float[] offsets;
        
        public NoiseGenerator(int seed)
        {
            this.seed = seed;
            random = new System.Random(seed);
            offsets = GenerateOffsets(10); // Increased from 8 to 10 to accommodate border noise
        }
        
        // Get consistent noise value for any world position
        public float GetNoise(float x, float z, int offsetIndex, float scale)
        {
            int index = offsetIndex * 2;
            if (index >= offsets.Length)
            {
                Debug.LogError($"Offset index {offsetIndex} (array index {index}) is out of bounds. Array length: {offsets.Length}");
                return 0f;
            }
            
            float nx = (x + offsets[index]) * scale;
            float nz = (z + offsets[index + 1]) * scale;
            return Mathf.PerlinNoise(nx, nz);
        }
        
        // Get noise in a specific range
        public float GetNoise(float x, float z, int offsetIndex, float scale, float minValue, float maxValue)
        {
            float noise = GetNoise(x, z, offsetIndex, scale);
            return Mathf.Lerp(minValue, maxValue, noise);
        }
        
        // Get noise with multiple octaves for more detail
        public float GetOctaveNoise(float x, float z, int offsetIndex, float scale, int octaves, float persistence)
        {
            float total = 0;
            float frequency = 1;
            float amplitude = 1;
            float maxValue = 0;
            
            for(int i = 0; i < octaves; i++)
            {
                total += GetNoise(x * frequency, z * frequency, offsetIndex, scale) * amplitude;
                maxValue += amplitude;
                amplitude *= persistence;
                frequency *= 2;
            }
            
            return total / maxValue;
        }
        
        // Generate ridged noise (good for mountains)
        public float GetRidgedNoise(float x, float z, int offsetIndex, float scale)
        {
            float noise = GetNoise(x, z, offsetIndex, scale);
            return 1f - Mathf.Abs(noise - 0.5f) * 2f;
        }
        
        private float[] GenerateOffsets(int count)
        {
            float[] offsets = new float[count];
            for(int i = 0; i < count; i++)
            {
                offsets[i] = (float)(random.NextDouble() * 10000f);
            }
            return offsets;
        }
        
        // Get a new random value based on position (useful for feature generation)
        public float GetRandomValue(int x, int z)
        {
            return (float)new System.Random(HashCoordinates(x, z, seed)).NextDouble();
        }
        
        private int HashCoordinates(int x, int z, int seed)
        {
            int hash = seed;
            hash = hash * 31 + x;
            hash = hash * 31 + z;
            return hash;
        }
    }
} 