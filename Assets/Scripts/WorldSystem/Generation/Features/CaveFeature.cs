using UnityEngine;
using VoxelGame.WorldSystem.Generation.Core;

namespace VoxelGame.WorldSystem.Generation.Features
{
    /// <summary>
    /// Settings specific to cave generation.
    /// These values can be different per biome and will be blended.
    /// </summary>
    public class CaveSettings : FeatureSettings
    {
        public float frequency = 0.03f;     // How often caves appear
        public float size = 1f;             // Size of cave tunnels
        public float windiness = 1f;        // How much caves wind and twist
        public int minHeight = 8;           // Minimum height for caves
        public int maxHeight = 120;         // Maximum height for caves
        public float threshold = 0.55f;     // Threshold for cave formation (higher = fewer caves)
    }

    /// <summary>
    /// Handles cave generation across all biomes.
    /// The actual cave system is continuous, but its characteristics change based on biome settings.
    /// </summary>
    public class CaveFeature : WorldFeature
    {
        private readonly CaveSettings caveSettings;
        private const int CAVE_NOISE_INDEX = 5;  // Unique noise index for caves
        private const int CAVE_SHAPE_INDEX = 6;  // Additional noise for cave shape

        public CaveFeature(CaveSettings settings) : base(settings)
        {
            this.caveSettings = settings;
        }

        public override void Apply(Chunk chunk, NoiseGenerator noise)
        {
            // Skip if chunk is above or below cave generation range
            int worldY = chunk.Position.y * Chunk.ChunkSize;
            if (worldY > caveSettings.maxHeight || worldY + Chunk.ChunkSize < caveSettings.minHeight)
                return;

            // Generate caves
            for (int x = 0; x < Chunk.ChunkSize; x++)
            for (int y = 0; y < Chunk.ChunkSize; y++)
            for (int z = 0; z < Chunk.ChunkSize; z++)
            {
                float worldX = chunk.Position.x * Chunk.ChunkSize + x;
                float worldZ = chunk.Position.z * Chunk.ChunkSize + z;
                int absoluteY = worldY + y;

                if (ShouldGenerateCave(worldX, absoluteY, worldZ, noise))
                {
                    chunk.SetBlock(x, y, z, null); // null = air
                }
            }
        }

        private bool ShouldGenerateCave(float x, float y, float z, NoiseGenerator noise)
        {
            // Skip if outside height range
            if (y < caveSettings.minHeight || y > caveSettings.maxHeight)
                return false;

            // Get base cave noise using octaves for more natural-looking caves
            float caveNoise = noise.GetOctaveNoise(
                x * caveSettings.frequency,
                z * caveSettings.frequency,
                CAVE_NOISE_INDEX,
                caveSettings.size,
                3,  // Use 3 octaves
                0.5f  // Persistence
            );

            // Add winding variation using 3D noise
            float windingNoise = noise.GetNoise(
                x * caveSettings.frequency * 2,
                z * caveSettings.frequency * 2,
                CAVE_SHAPE_INDEX,
                caveSettings.windiness
            );

            // Combine noises
            float combinedNoise = (caveNoise + windingNoise * 0.5f) / 1.5f;

            // Apply height falloff near limits
            float heightFactor = GetHeightFalloff(y);
            combinedNoise *= heightFactor;

            // Return true if we should carve a cave here
            return combinedNoise > caveSettings.threshold;
        }

        private float GetHeightFalloff(float y)
        {
            float bottomFalloff = Mathf.SmoothStep(0, 1, 
                (y - caveSettings.minHeight) / 20f);
            
            float topFalloff = Mathf.SmoothStep(1, 0, 
                (y - (caveSettings.maxHeight - 20)) / 20f);

            return Mathf.Min(bottomFalloff, topFalloff);
        }
    }
} 