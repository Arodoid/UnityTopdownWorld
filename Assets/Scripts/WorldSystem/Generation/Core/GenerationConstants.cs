using UnityEngine;

namespace VoxelGame.WorldSystem.Generation.Core
{
    /// <summary>
    /// Central location for all generation-related constants.
    /// Each feature can reference these defaults but can be overridden by biome configs.
    /// No logic, just values.
    /// </summary>
    public static class GenerationConstants
    {
        public static class World
        {
            public const int SEA_LEVEL = 64;
            public const int MIN_HEIGHT = 0;
            public const int MAX_HEIGHT = 256;
            public const int CHUNK_SIZE = 16;
        }

        public static class Noise
        {
            public static class Terrain
            {
                public const float BASE_SCALE = 0.01f;
                public const float HEIGHT_MULTIPLIER = 16f;
                public const float HEIGHT_OFFSET = 64f;
            }

            public static class Mountains
            {
                public const float SCALE = 0.01f;
                public const float HEIGHT_MULTIPLIER = 64f;
                public const float THRESHOLD = 0.6f;  // When to start generating mountains
            }

            public static class Lakes
            {
                public const float SCALE = 0.03f;
                public const float THRESHOLD = 0.6f;  // Higher = fewer lakes
                public const int DEPTH = 8;
            }
            public static class Caves
            {
                public const float SCALE = 0.03f;
                public const float THRESHOLD = 0.55f; // Higher = fewer caves
                public const int MIN_HEIGHT = 8;
                public const int MAX_HEIGHT = 120;
            }

            public static class Biomes
            {
                public const float SCALE = 0.0001f;
                public const float BLEND_RANGE = 0.35f;
            }
        }

        public static class Features
        {
            public static class Trees
            {
                public const float DENSITY = 0.1f;    // Higher = more trees
                public const int MIN_HEIGHT = 4;
                public const int MAX_HEIGHT = 8;
            }

            public static class Ores
            {
                public const float SCALE = 0.05f;
                public const float DENSITY = 0.6f;    // Higher = more ore veins
                
                public static class Height
                {
                    public const int COAL_MAX = 128;
                    public const int IRON_MAX = 64;
                    public const int GOLD_MAX = 32;
                    public const int DIAMOND_MAX = 16;
                }
            }

            public static class Flowers
            {
                public const float DENSITY = 0.05f;   // Higher = more flowers
                public const float SCALE = 0.1f;      // Scale of flower patches
            }
        }
    }
}