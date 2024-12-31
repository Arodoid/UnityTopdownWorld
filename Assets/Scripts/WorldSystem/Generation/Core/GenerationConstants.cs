using UnityEngine;

namespace VoxelGame.WorldSystem.Generation.Core
{
    public static class GenerationConstants
    {
        // World Settings
        public const int SEA_LEVEL = 64;
        public const int MIN_HEIGHT = 0;
        public const int MAX_HEIGHT = 256;
        
        // Noise Settings
        public static class Noise
        {
            // Biome Noise
            public const float BIOME_SCALE = 0.005f;
            public const float TEMPERATURE_OFFSET = 0.5f;
            
            // Terrain Noise
            public const float BASE_TERRAIN_SCALE = 0.03f;
            public const float HILLS_SCALE = 0.01f;
            public const float MOUNTAINS_SCALE = 0.005f;
            public const float ROUGHNESS_SCALE = 0.1f;
            
            // Terrain Strength Modifiers
            public const float BASE_STRENGTH = 1f;
            public const float HILLS_STRENGTH = 4f;
            public const float MOUNTAINS_STRENGTH = 32f;
            public const float ROUGHNESS_STRENGTH = 2f;
        }
        
        // Biome Thresholds
        public static class Biomes
        {
            public const float COLD_THRESHOLD = 0.3f;
            public const float TEMPERATE_THRESHOLD = 0.6f;
            public const float HOT_THRESHOLD = 0.7f;
            
            public const float MOUNTAIN_THRESHOLD = 0.6f;
            public const float DESERT_HEIGHT_MODIFIER = 0.8f;
            public const float SNOW_HEIGHT_MODIFIER = 1.1f;
        }
    }
} 