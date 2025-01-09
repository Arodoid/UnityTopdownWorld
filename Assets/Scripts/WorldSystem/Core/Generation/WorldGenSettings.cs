using UnityEngine;
using WorldSystem.Data;

namespace WorldSystem.Generation
{
    [CreateAssetMenu(fileName = "WorldGenSettings", menuName = "WorldGen/Settings")]
    public class WorldGenSettings : ScriptableObject
    {
        [Header("Generation Toggles")]
        public bool EnableTerrainHeight = true;
        public bool Enable3DTerrain = true;
        public bool EnableWater = true;

        [Header("Basic World Settings")]
        [Tooltip("Size of each chunk in blocks (32x32 horizontal area)")]
        public int ChunkSize = 32;
        public int WorldHeight = 256;
        public float SeaLevel = 64;
        
        [Header("Biome Settings")]
        public NoiseSettings BiomeNoiseSettings = new NoiseSettings
        {
            Scale = 1000,
            Amplitude = 1,     
            Frequency = 0.01f, 
            Octaves = 4,      
            Persistence = 0.5f,
            Lacunarity = 2,    
            Seed = 42         
        };

        public BiomeSettings[] Biomes;
        public int BiomeBlendDistance = 4;
        
        [Header("Global Layer Settings")]
        public float DefaultLayerDepth = 4f;
        public BlockType DefaultSubsurfaceBlock = BlockType.Dirt;
        public BlockType DefaultDeepBlock = BlockType.Stone;

        [Header("Global 3D Terrain Settings")]
        public NoiseSettings GlobalDensityNoise = new NoiseSettings
        {
            Scale = 50f,
            Amplitude = 1f,
            Frequency = 0.03f,
            Octaves = 3,
            Persistence = 0.5f,
            Lacunarity = 2f,
            Seed = 42
        };

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
                        Continentalness = 0.1f,
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
                        DensitySettings = new TerrainDensitySettings
                        {
                            DensityBias = 0.8f,            // Very solid
                            HeightScale = 0.2f,            // Minimal height influence
                            HeightOffset = -0.1f,          // Shift density down
                            VerticalBias = 4.0f,           // Very sharp vertical falloff
                            GradientStartHeight = 32f      // Start low
                        },
                        Layer1 = new BiomeBlockLayer { BlockType = BlockType.Sand, MinDepth = 0, MaxDepth = 3 },
                        Layer2 = new BiomeBlockLayer { BlockType = BlockType.Stone, MinDepth = 3, MaxDepth = float.MaxValue },
                        LayerCount = 2,
                        DefaultSurfaceBlock = BlockType.Sand,
                        UnderwaterSurfaceBlock = BlockType.Sand,
                        UnderwaterThreshold = 2
                    },

                    // Beach/Coastal
                    new BiomeSettings
                    {
                        BiomeId = 1,
                        Temperature = 0.5f,
                        Humidity = 0.5f,
                        Continentalness = 0.3f,
                        HeightSettings = new BiomeHeightSettings
                        {
                            BaseHeight = 62,
                            HeightVariation = 8,
                            TerrainNoiseSettings = new NoiseSettings
                            {
                                Scale = 100,
                                Amplitude = 1f,
                                Frequency = 0.02f,
                                Octaves = 4,
                                Persistence = 0.5f,
                                Lacunarity = 2f,
                                Seed = 42
                            },
                            SeaLevelOffset = -2
                        },
                        DensitySettings = new TerrainDensitySettings
                        {
                            DensityBias = 0.3f,            // Mostly solid
                            HeightScale = 0.4f,            // Moderate height influence
                            HeightOffset = 0f,             // No offset
                            VerticalBias = 2.5f,           // Sharper vertical falloff
                            GradientStartHeight = 64f      // Start at sea level
                        },
                        Layer1 = new BiomeBlockLayer { BlockType = BlockType.Sand, MinDepth = 0, MaxDepth = 4 },
                        Layer2 = new BiomeBlockLayer { BlockType = BlockType.Stone, MinDepth = 4, MaxDepth = float.MaxValue },
                        LayerCount = 2,
                        DefaultSurfaceBlock = BlockType.Sand,
                        UnderwaterSurfaceBlock = BlockType.Sand,
                        UnderwaterThreshold = 1
                    },

                    // Plains
                    new BiomeSettings
                    {
                        BiomeId = 2,
                        Temperature = 0.5f,
                        Humidity = 0.5f,
                        Continentalness = 0.6f,
                        HeightSettings = new BiomeHeightSettings
                        {
                            BaseHeight = 68,
                            HeightVariation = 12,
                            TerrainNoiseSettings = new NoiseSettings
                            {
                                Scale = 120,
                                Amplitude = 1f,
                                Frequency = 0.02f,
                                Octaves = 4,
                                Persistence = 0.5f,
                                Lacunarity = 2f,
                                Seed = 42
                            },
                            SeaLevelOffset = 4
                        },
                        DensitySettings = new TerrainDensitySettings
                        {
                            DensityBias = 0.3f,            // Mostly solid
                            HeightScale = 0.4f,            // Moderate height influence
                            HeightOffset = 0f,             // No offset
                            VerticalBias = 2.5f,           // Sharper vertical falloff
                            GradientStartHeight = 64f      // Start at sea level
                        },
                        Layer1 = new BiomeBlockLayer { BlockType = BlockType.Dirt, MinDepth = 0, MaxDepth = 4 },
                        Layer2 = new BiomeBlockLayer { BlockType = BlockType.Stone, MinDepth = 4, MaxDepth = float.MaxValue },
                        LayerCount = 2,
                        DefaultSurfaceBlock = BlockType.Grass,
                        UnderwaterSurfaceBlock = BlockType.Dirt,
                        UnderwaterThreshold = 2
                    },

                    // Mountains
                    new BiomeSettings
                    {
                        BiomeId = 3,
                        Temperature = 0.5f,
                        Humidity = 0.5f,
                        Continentalness = 0.9f,
                        HeightSettings = new BiomeHeightSettings
                        {
                            BaseHeight = 90,
                            HeightVariation = 45,
                            TerrainNoiseSettings = new NoiseSettings
                            {
                                Scale = 150,
                                Amplitude = 1.2f,
                                Frequency = 0.015f,
                                Octaves = 5,
                                Persistence = 0.6f,
                                Lacunarity = 2.2f,
                                Seed = 42
                            },
                            SeaLevelOffset = 26
                        },
                        DensitySettings = new TerrainDensitySettings
                        {
                            DensityBias = -0.3f,           // More caves
                            HeightScale = 0.6f,            // Strong height influence
                            HeightOffset = 0.2f,           // Shift density up slightly
                            VerticalBias = 1.2f,           // Gradual vertical falloff
                            GradientStartHeight = 80f      // Start vertical falloff higher up
                        },
                        Layer1 = new BiomeBlockLayer { BlockType = BlockType.Stone, MinDepth = 0, MaxDepth = float.MaxValue },
                        LayerCount = 1,
                        DefaultSurfaceBlock = BlockType.Stone,
                        UnderwaterSurfaceBlock = BlockType.Stone,
                        UnderwaterThreshold = 2
                    }
                };
            }
        }
    }
} 