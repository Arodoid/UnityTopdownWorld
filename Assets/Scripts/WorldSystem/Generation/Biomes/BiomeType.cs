using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace VoxelGame.WorldSystem.Biomes
{
    public enum BiomeType
    {
        Grassland,
        Desert,
        Snow,
        // Add new biomes here
    }

    public class BiomeSettings
    {
        public BiomeType Type { get; private set; }
        public float MinTemperature { get; private set; }
        public float MaxTemperature { get; private set; }
        public float HeightMultiplier { get; private set; }
        public float HeightOffset { get; private set; }
        public int SurfaceDepth { get; private set; }
        public int SubsurfaceDepth { get; private set; }
        public Block SurfaceBlock { get; private set; }
        public Block SubsurfaceBlock { get; private set; }
        public Color Color { get; private set; }

        public BiomeSettings(
            BiomeType type,
            float minTemperature,
            float maxTemperature,
            float heightMultiplier,
            float heightOffset,
            int surfaceDepth,
            int subsurfaceDepth,
            Block surfaceBlock,
            Block subsurfaceBlock,
            Color color)
        {
            Type = type;
            MinTemperature = minTemperature;
            MaxTemperature = maxTemperature;
            HeightMultiplier = heightMultiplier;
            HeightOffset = heightOffset;
            SurfaceDepth = surfaceDepth;
            SubsurfaceDepth = subsurfaceDepth;
            SurfaceBlock = surfaceBlock;
            SubsurfaceBlock = subsurfaceBlock;
            Color = color;
        }
    }

    public static class BiomeRegistry
    {
        private static readonly Dictionary<BiomeType, BiomeSettings> Biomes = new Dictionary<BiomeType, BiomeSettings>
        {
            // Add all biomes here in one place
            {
                BiomeType.Grassland,
                new BiomeSettings(
                    BiomeType.Grassland,
                    0.3f,
                    0.7f,
                    1.0f,
                    0f,
                    1,
                    4,
                    Block.Types.Grass,
                    Block.Types.Dirt,
                    new Color(0.2f, 0.8f, 0.2f)
                )
            },
            {
                BiomeType.Desert,
                new BiomeSettings(
                    BiomeType.Desert,
                    0.7f,
                    1.0f,
                    0.8f,
                    -5f,
                    4,
                    8,
                    Block.Types.Sand,
                    Block.Types.Sand,
                    new Color(0.9f, 0.9f, 0.2f)
                )
            },
            {
                BiomeType.Snow,
                new BiomeSettings(
                    BiomeType.Snow,
                    0.0f,
                    0.3f,
                    1.1f,
                    2f,
                    1,
                    4,
                    Block.Types.Snow,
                    Block.Types.Dirt,
                    Color.white
                )
            }
            // Add new biomes here by copying and modifying an existing block
        };

        public static BiomeSettings GetBiomeSettings(float temperature)
        {
            return Biomes.Values
                .FirstOrDefault(b => temperature >= b.MinTemperature && temperature <= b.MaxTemperature)
                ?? Biomes[BiomeType.Grassland]; // Default biome
        }

        public static BiomeType GetBiomeType(float temperature)
        {
            return GetBiomeSettings(temperature).Type;
        }

        public static Color GetBiomeColor(BiomeType biomeType)
        {
            return Biomes.TryGetValue(biomeType, out var settings) 
                ? settings.Color 
                : Color.magenta;
        }

        public static BiomeSettings GetSettings(BiomeType type)
        {
            return Biomes.TryGetValue(type, out var settings) 
                ? settings 
                : Biomes[BiomeType.Grassland];
        }

        public static IEnumerable<BiomeSettings> GetAllBiomes() => Biomes.Values;
    }
}