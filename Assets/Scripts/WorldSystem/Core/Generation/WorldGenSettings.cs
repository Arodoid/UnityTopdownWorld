using UnityEngine;
using WorldSystem.Data;

namespace WorldSystem.Generation
{
    [CreateAssetMenu(fileName = "WorldGenSettings", menuName = "WorldGen/Settings")]
    public class WorldGenSettings : ScriptableObject
    {
        [Header("Basic World Settings")]
        [Tooltip("Size of each chunk in blocks (32x32 horizontal area)")]
        public int ChunkSize = 32;

        [Tooltip("Maximum height of the world in blocks")]
        public int WorldHeight = 256;

        [Tooltip("Height at which water appears")]
        public float SeaLevel = 64;
        
        [Header("Biome Settings")]
        [Tooltip("Controls how biomes are distributed across the world")]
        public NoiseSettings BiomeNoiseSettings = new NoiseSettings
        {
            Scale = 1000,      // Much larger value for smaller biomes
            Amplitude = 1,     
            Frequency = 0.01f, 
            Octaves = 4,      
            Persistence = 0.5f,
            Lacunarity = 2,    
            Seed = 42         
        };

        [Tooltip("Array of all biome configurations")]
        public BiomeSettings[] Biomes;

        [Tooltip("How far biomes blend together")]
        public int BiomeBlendDistance = 4;

        [Tooltip("Default biome ID to use if no other biome is suitable")]
        public int DefaultBiomeId = 0;
        
        [Header("Global Layer Settings")]
        [Tooltip("Default depth of the surface layer")]
        public float DefaultLayerDepth = 4f;

        [Tooltip("Block type used for the layer just below surface")]
        public BlockType DefaultSubsurfaceBlock = BlockType.Dirt;

        [Tooltip("Block type used for the deepest layer")]
        public BlockType DefaultDeepBlock = BlockType.Stone;

        private void OnValidate()
        {
            if (Biomes == null || Biomes.Length == 0)
            {
                Biomes = new BiomeSettings[]
                {
                    // Deep Ocean Biome
                    new BiomeSettings
                    {
                        BiomeId = 0,
                        Temperature = 0.5f,
                        Humidity = 0.5f,
                        Continentalness = 0.1f,  // Very low for deep ocean
                        HeightSettings = new BiomeHeightSettings
                        {
                            BaseHeight = 20,
                            HeightVariation = 15,
                            TerrainNoiseSettings = new NoiseSettings
                            {
                                Scale = 80,
                                Amplitude = 1.2f,
                                Frequency = 0.025f,
                                Octaves = 6,
                                Persistence = 0.6f,
                                Lacunarity = 2.5f,
                                Seed = 42
                            },
                            SeaLevelOffset = -20
                        },
                        Layer1 = new BiomeBlockLayer
                        {
                            BlockType = BlockType.Sand,
                            MinDepth = 0,
                            MaxDepth = 3
                        },
                        Layer2 = new BiomeBlockLayer
                        {
                            BlockType = BlockType.Stone,
                            MinDepth = 3,
                            MaxDepth = float.MaxValue
                        },
                        LayerCount = 2,
                        DefaultSurfaceBlock = BlockType.Sand,
                        UnderwaterSurfaceBlock = BlockType.Sand,
                        UnderwaterThreshold = 2
                    },

                    // Beach Biome
                    new BiomeSettings
                    {
                        BiomeId = 1,
                        Temperature = 0.5f,
                        Humidity = 0.5f,
                        Continentalness = 0.3f,  // Low-medium for beaches
                        HeightSettings = new BiomeHeightSettings
                        {
                            BaseHeight = 62,
                            HeightVariation = 4,
                            TerrainNoiseSettings = new NoiseSettings
                            {
                                Scale = 40,
                                Amplitude = 1.0f,
                                Frequency = 0.05f,
                                Octaves = 4,
                                Persistence = 0.5f,
                                Lacunarity = 2.0f,
                                Seed = 42
                            },
                            SeaLevelOffset = 0
                        },
                        Layer1 = new BiomeBlockLayer
                        {
                            BlockType = BlockType.Sand,
                            MinDepth = 0,
                            MaxDepth = 4
                        },
                        Layer2 = new BiomeBlockLayer
                        {
                            BlockType = BlockType.Stone,
                            MinDepth = 4,
                            MaxDepth = float.MaxValue
                        },
                        LayerCount = 2,
                        DefaultSurfaceBlock = BlockType.Sand,
                        UnderwaterSurfaceBlock = BlockType.Sand,
                        UnderwaterThreshold = 2
                    },

                    // Plains Biome
                    new BiomeSettings
                    {
                        BiomeId = 2,
                        Temperature = 0.5f,
                        Humidity = 0.5f,
                        Continentalness = 0.6f,  // Medium-high for plains
                        HeightSettings = new BiomeHeightSettings
                        {
                            BaseHeight = 68,
                            HeightVariation = 16,
                            TerrainNoiseSettings = new NoiseSettings
                            {
                                Scale = 100,
                                Amplitude = 1.2f,
                                Frequency = 0.03f,
                                Octaves = 5,
                                Persistence = 0.5f,
                                Lacunarity = 2.2f,
                                Seed = 42
                            },
                            SeaLevelOffset = 4
                        },
                        Layer1 = new BiomeBlockLayer
                        {
                            BlockType = BlockType.Dirt,
                            MinDepth = 0,
                            MaxDepth = 4
                        },
                        Layer2 = new BiomeBlockLayer
                        {
                            BlockType = BlockType.Stone,
                            MinDepth = 4,
                            MaxDepth = float.MaxValue
                        },
                        LayerCount = 2,
                        DefaultSurfaceBlock = BlockType.Grass,
                        UnderwaterSurfaceBlock = BlockType.Dirt,
                        UnderwaterThreshold = 2
                    },

                    // Mountains Biome
                    new BiomeSettings
                    {
                        BiomeId = 3,
                        Temperature = 0.5f,
                        Humidity = 0.5f,
                        Continentalness = 0.9f,  // Very high for mountains
                        HeightSettings = new BiomeHeightSettings
                        {
                            BaseHeight = 90,
                            HeightVariation = 120,
                            TerrainNoiseSettings = new NoiseSettings
                            {
                                Scale = 200,
                                Amplitude = 2.0f,
                                Frequency = 0.015f,
                                Octaves = 7,
                                Persistence = 0.7f,
                                Lacunarity = 2.8f,
                                Seed = 42
                            },
                            SeaLevelOffset = 20
                        },
                        Layer1 = new BiomeBlockLayer
                        {
                            BlockType = BlockType.Stone,
                            MinDepth = 0,
                            MaxDepth = float.MaxValue
                        },
                        LayerCount = 1,
                        DefaultSurfaceBlock = BlockType.Stone,
                        UnderwaterSurfaceBlock = BlockType.Stone,
                        UnderwaterThreshold = 2
                    }
                };
            }

            // Validate basic settings
            ChunkSize = Mathf.Max(16, ChunkSize);
            WorldHeight = Mathf.Max(64, WorldHeight);
            SeaLevel = Mathf.Clamp(SeaLevel, 0, WorldHeight);
            BiomeBlendDistance = Mathf.Max(1, BiomeBlendDistance);
            DefaultLayerDepth = Mathf.Max(1, DefaultLayerDepth);
        }
    }
} 