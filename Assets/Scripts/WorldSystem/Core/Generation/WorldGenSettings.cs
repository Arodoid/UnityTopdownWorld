using UnityEngine;
using WorldSystem.Data;

namespace WorldSystem.Generation
{
    [CreateAssetMenu(fileName = "WorldGenSettings", menuName = "WorldGen/Settings")]
    public class WorldGenSettings : ScriptableObject
    {
        [Header("Generation Toggles")]
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
                    new BiomeSettings
                    {
                        BiomeId = 0,
                        Temperature = 0.5f,
                        Humidity = 0.5f,
                        Continentalness = 0.5f,
                        DensitySettings = new TerrainDensitySettings
                        {
                            DeepStart = 0,
                            CaveStart = 40,
                            CaveEnd = 60,
                            SurfaceStart = 80,
                            SurfaceEnd = 100,
                            DeepBias = 1f,
                            CaveBias = 0f,
                            SurfaceBias = 0.5f,
                            DeepTransitionScale = 0.1f,
                            CaveTransitionScale = 1f,
                            AirTransitionScale = 0.1f
                        },
                        PrimaryBlock = BlockType.Dirt,
                        SecondaryBlock = BlockType.Stone,
                        TopBlock = BlockType.Grass,
                        UnderwaterBlock = BlockType.Sand,
                        UnderwaterThreshold = 2f
                    }
                };
            }
        }
    }
} 