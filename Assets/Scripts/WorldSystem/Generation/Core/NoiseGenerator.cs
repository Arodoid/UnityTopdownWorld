using UnityEngine;

namespace VoxelGame.WorldSystem.Generation.Core
{
    /// <summary>
    /// Provides deterministic noise generation for world features.
    /// This class is a pure utility - it has no knowledge of world generation logic,
    /// biomes, or features. It simply provides noise values based on input coordinates.
    /// </summary>
    public class NoiseGenerator
    {
        private readonly int seed;
        private readonly System.Random random;
        private readonly float[] offsets;
        private const int OFFSET_COUNT = 16;

        public NoiseGenerator(int seed)
        {
            this.seed = seed;
            random = new System.Random(seed);
            offsets = GenerateOffsets(OFFSET_COUNT);
        }

        /// <summary>
        /// Gets a noise value between 0 and 1 for any world position.
        /// </summary>
        /// <param name="x">World X coordinate</param>
        /// <param name="z">World Z coordinate</param>
        /// <param name="offsetIndex">Index for consistent but varied noise patterns</param>
        /// <param name="scale">Scale of the noise (lower = more stretched)</param>
        /// <returns>Noise value between 0 and 1</returns>
        public float GetNoise(float x, float z, int offsetIndex, float scale)
        {
            // Wrap offset index to prevent out of bounds
            int index = (offsetIndex * 2) % offsets.Length;
            
            // Apply offsets to coordinates for varied patterns
            float nx = (x + offsets[index]) * scale;
            float nz = (z + offsets[index + 1]) * scale;
            
            return Mathf.PerlinNoise(nx, nz);
        }

        /// <summary>
        /// Gets noise value mapped to a specific range.
        /// </summary>
        public float GetNoise(float x, float z, int offsetIndex, float scale, float minValue, float maxValue)
        {
            float noise = GetNoise(x, z, offsetIndex, scale);
            return Mathf.Lerp(minValue, maxValue, noise);
        }

        /// <summary>
        /// Gets noise with multiple octaves for more natural-looking results.
        /// </summary>
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

        /// <summary>
        /// Gets ridged noise - good for mountains and terrain features.
        /// Creates sharp ridges in the noise pattern.
        /// </summary>
        public float GetRidgedNoise(float x, float z, int offsetIndex, float scale)
        {
            float noise = GetNoise(x, z, offsetIndex, scale);
            return 1f - Mathf.Abs(noise - 0.5f) * 2f;
        }

        /// <summary>
        /// Gets a random value based on world position - useful for feature placement.
        /// </summary>
        public float GetRandomValue(int x, int z)
        {
            return (float)new System.Random(HashCoordinates(x, z, seed)).NextDouble();
        }

        /// <summary>
        /// Generates consistent random offsets for noise variation.
        /// </summary>
        private float[] GenerateOffsets(int count)
        {
            float[] offsets = new float[count];
            for(int i = 0; i < count; i++)
            {
                offsets[i] = (float)(random.NextDouble() * 10000f);
            }
            return offsets;
        }

        /// <summary>
        /// Creates a deterministic hash from world coordinates.
        /// </summary>
        private int HashCoordinates(int x, int z, int seed)
        {
            int hash = seed;
            hash = hash * 31 + x;
            hash = hash * 31 + z;
            return hash;
        }

        /// <summary>
        /// Gets 3D noise - useful for caves and other 3D features.
        /// </summary>
        public float Get3DNoise(float x, float y, float z, int offsetIndex, float scale)
        {
            // Wrap offset index
            int index = (offsetIndex * 3) % offsets.Length;
            
            // Create three 2D noise samples and blend them
            float xy = GetNoise(x, y, offsetIndex, scale);
            float xz = GetNoise(x, z, offsetIndex + 1, scale);
            float yz = GetNoise(y, z, offsetIndex + 2, scale);
            
            return (xy + xz + yz) / 3f;
        }
    }
}