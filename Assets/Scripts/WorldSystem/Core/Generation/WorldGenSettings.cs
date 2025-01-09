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
            // Ensure we have at least the default biomes
            if (Biomes == null || Biomes.Length == 0)
            {
                Biomes = new BiomeSettings[]
                {
                    // Deep Ocean Biome
                    new BiomeSettings
                    {
                        BiomeId = 0,
                        Temperature = 0.5f,
                        Humidity = 0.8f,
                        HeightSettings = new BiomeHeightSettings
                        {
                            BaseHeight = 45,
                            HeightVariation = 15,
                            TerrainNoiseSettings = new NoiseSettings
                            {
                                Scale = 80,          // Smaller scale for more variation
                                Amplitude = 1.2f,    // Increased amplitude for more dramatic features
                                Frequency = 0.025f,  // Higher frequency for more frequent changes
                                Octaves = 6,         // More octaves for additional detail
                                Persistence = 0.6f,  // Higher persistence for more pronounced features
                                Lacunarity = 2.5f,   // Increased lacunarity for more variation in detail
                                Seed = 42
                            },
                            SeaLevelOffset = -20
                        },
                        Layer1 = new BiomeBlockLayer
                        {
                            BlockType = BlockType.Sand,
                            MinDepth = 0,
                            MaxDepth = 3,
                            LayerNoise = new NoiseSettings
                            {
                                Scale = 20,
                                Amplitude = 1,
                                Frequency = 0.1f,
                                Octaves = 2,
                                Persistence = 0.5f,
                                Lacunarity = 2,
                                Seed = 43
                            }
                        },
                        Layer2 = new BiomeBlockLayer
                        {
                            BlockType = BlockType.Stone,
                            MinDepth = 3,
                            MaxDepth = float.MaxValue,
                            LayerNoise = new NoiseSettings()
                        },
                        LayerCount = 2,
                        DefaultSurfaceBlock = BlockType.Sand,
                        UnderwaterSurfaceBlock = BlockType.Sand,
                        UnderwaterThreshold = 2
                    },

                    // Plains Biome
                    new BiomeSettings
                    {
                        BiomeId = 1,
                        Temperature = 0.5f,
                        Humidity = 0.4f,
                        HeightSettings = new BiomeHeightSettings
                        {
                            BaseHeight = 64,
                            HeightVariation = 8,
                            TerrainNoiseSettings = new NoiseSettings
                            {
                                Scale = 20,
                                Amplitude = 1,
                                Frequency = 0.05f,
                                Octaves = 5,
                                Persistence = 0.5f,
                                Lacunarity = 2.2f,
                                Seed = 42
                            },
                            SeaLevelOffset = 2
                        },
                        Layer1 = new BiomeBlockLayer
                        {
                            BlockType = BlockType.Dirt,
                            MinDepth = 0,
                            MaxDepth = 4,
                            LayerNoise = new NoiseSettings
                            {
                                Scale = 15,
                                Amplitude = 0.5f,
                                Frequency = 0.08f,
                                Octaves = 2,
                                Persistence = 0.5f,
                                Lacunarity = 2,
                                Seed = 44
                            }
                        },
                        Layer2 = new BiomeBlockLayer
                        {
                            BlockType = BlockType.Stone,
                            MinDepth = 4,
                            MaxDepth = float.MaxValue,
                            LayerNoise = new NoiseSettings()
                        },
                        LayerCount = 2,
                        DefaultSurfaceBlock = BlockType.Grass,
                        UnderwaterSurfaceBlock = BlockType.Sand,
                        UnderwaterThreshold = 2
                    },

                    // Mountains Biome
                    new BiomeSettings
                    {
                        BiomeId = 2,
                        Temperature = 0.3f,
                        Humidity = 0.3f,
                        HeightSettings = new BiomeHeightSettings
                        {
                            BaseHeight = 80,
                            HeightVariation = 90,
                            TerrainNoiseSettings = new NoiseSettings
                            {
                                Scale = 120,
                                Amplitude = 1.5f,
                                Frequency = 0.015f,
                                Octaves = 7,
                                Persistence = 0.7f,
                                Lacunarity = 2.8f,
                                Seed = 42
                            },
                            SeaLevelOffset = 15
                        },
                        Layer1 = new BiomeBlockLayer
                        {
                            BlockType = BlockType.Stone,
                            MinDepth = 0,
                            MaxDepth = float.MaxValue,
                            LayerNoise = new NoiseSettings
                            {
                                Scale = 30,
                                Amplitude = 1,
                                Frequency = 0.05f,
                                Octaves = 3,
                                Persistence = 0.6f,
                                Lacunarity = 2.2f,
                                Seed = 45
                            }
                        },
                        LayerCount = 1,
                        DefaultSurfaceBlock = BlockType.Stone,
                        UnderwaterSurfaceBlock = BlockType.Stone,
                        UnderwaterThreshold = 2
                    },

                    // Desert Biome
                    new BiomeSettings
                    {
                        BiomeId = 3,
                        Temperature = 0.8f,
                        Humidity = 0.1f,
                        HeightSettings = new BiomeHeightSettings
                        {
                            BaseHeight = 62,
                            HeightVariation = 25,
                            TerrainNoiseSettings = new NoiseSettings
                            {
                                Scale = 40,
                                Amplitude = 1.2f,
                                Frequency = 0.04f,
                                Octaves = 4,
                                Persistence = 0.55f,
                                Lacunarity = 2.4f,
                                Seed = 42
                            },
                            SeaLevelOffset = 1
                        },
                        Layer1 = new BiomeBlockLayer
                        {
                            BlockType = BlockType.Sand,
                            MinDepth = 0,
                            MaxDepth = 12,
                            LayerNoise = new NoiseSettings
                            {
                                Scale = 25,
                                Amplitude = 1,
                                Frequency = 0.06f,
                                Octaves = 3,
                                Persistence = 0.5f,
                                Lacunarity = 2.1f,
                                Seed = 46
                            }
                        },
                        Layer2 = new BiomeBlockLayer
                        {
                            BlockType = BlockType.Sandstone,
                            MinDepth = 12,
                            MaxDepth = float.MaxValue,
                            LayerNoise = new NoiseSettings()
                        },
                        LayerCount = 2,
                        DefaultSurfaceBlock = BlockType.Sand,
                        UnderwaterSurfaceBlock = BlockType.Sand,
                        UnderwaterThreshold = 2
                    },

                    // Forest Biome
                    new BiomeSettings
                    {
                        BiomeId = 4,
                        Temperature = 0.6f,
                        Humidity = 0.6f,
                        HeightSettings = new BiomeHeightSettings
                        {
                            BaseHeight = 68,
                            HeightVariation = 30,
                            TerrainNoiseSettings = new NoiseSettings
                            {
                                Scale = 70,
                                Amplitude = 1.3f,
                                Frequency = 0.035f,
                                Octaves = 6,
                                Persistence = 0.65f,
                                Lacunarity = 2.3f,
                                Seed = 42
                            },
                            SeaLevelOffset = 4
                        },
                        Layer1 = new BiomeBlockLayer
                        {
                            BlockType = BlockType.Dirt,
                            MinDepth = 0,
                            MaxDepth = 6,
                            LayerNoise = new NoiseSettings
                            {
                                Scale = 20,
                                Amplitude = 0.8f,
                                Frequency = 0.07f,
                                Octaves = 3,
                                Persistence = 0.5f,
                                Lacunarity = 2,
                                Seed = 47
                            }
                        },
                        Layer2 = new BiomeBlockLayer
                        {
                            BlockType = BlockType.Stone,
                            MinDepth = 6,
                            MaxDepth = float.MaxValue,
                            LayerNoise = new NoiseSettings()
                        },
                        LayerCount = 2,
                        DefaultSurfaceBlock = BlockType.Grass,
                        UnderwaterSurfaceBlock = BlockType.Dirt,
                        UnderwaterThreshold = 2
                    },

                    // Tundra Biome
                    new BiomeSettings
                    {
                        BiomeId = 5,
                        Temperature = 0.1f,
                        Humidity = 0.3f,
                        HeightSettings = new BiomeHeightSettings
                        {
                            BaseHeight = 66,
                            HeightVariation = 15,
                            TerrainNoiseSettings = new NoiseSettings
                            {
                                Scale = 50,
                                Amplitude = 1.1f,
                                Frequency = 0.045f,
                                Octaves = 5,
                                Persistence = 0.5f,
                                Lacunarity = 2.2f,
                                Seed = 42
                            },
                            SeaLevelOffset = 2
                        },
                        Layer1 = new BiomeBlockLayer
                        {
                            BlockType = BlockType.Snow,
                            MinDepth = 0,
                            MaxDepth = 3,
                            LayerNoise = new NoiseSettings
                            {
                                Scale = 15,
                                Amplitude = 0.7f,
                                Frequency = 0.09f,
                                Octaves = 2,
                                Persistence = 0.45f,
                                Lacunarity = 2,
                                Seed = 48
                            }
                        },
                        Layer2 = new BiomeBlockLayer
                        {
                            BlockType = BlockType.Stone,
                            MinDepth = 3,
                            MaxDepth = float.MaxValue,
                            LayerNoise = new NoiseSettings()
                        },
                        LayerCount = 2,
                        DefaultSurfaceBlock = BlockType.Snow,
                        UnderwaterSurfaceBlock = BlockType.Ice,
                        UnderwaterThreshold = 1
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