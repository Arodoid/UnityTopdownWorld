using UnityEngine;
using VoxelGame.WorldSystem.Generation.Features;

namespace VoxelGame.WorldSystem.Generation.Biomes
{
    public enum BiomeType
    {
        Plains,
        Desert
        // Mountains,
        // Forest,
        // Tundra
    }

    public class PlainsBiome : BiomeBase
    {
        // Constructor sets "what this biome IS"
        public PlainsBiome()
        {
            Type = BiomeType.Plains;
            Name = "Plains";
            Color = Color.green;
            MinTemperature = 0.0f;  // Start from absolute cold
            MaxTemperature = 0.7f;
        }
        
        // Initialize sets "what this biome DOES"
        protected override void InitializeDefaultSettings()
        {
            terrainSettings = new TerrainSettings
            {
                enabled = true,
                scale = 1f,
                strength = 1f,
                baseHeight = 64f,
                baseVariation = 0f,
                generateHills = true,
                hillsHeight = 12f,
                hillsFrequency = 0.02f,
                generateMountains = false,
                surfaceBlock = Block.Types.Grass,
                subsurfaceBlock = Block.Types.Dirt,
                deepBlock = Block.Types.Stone,
                surfaceDepth = 1,
                subsurfaceDepth = 4
            };

            featureSettings[typeof(TerrainSettings)] = terrainSettings;
        }
    }

    public class DesertBiome : BiomeBase
    {
        // Constructor sets "what this biome IS"
        public DesertBiome()
        {
            Type = BiomeType.Desert;
            Name = "Desert";
            Color = new Color(0.76f, 0.7f, 0.5f); // Sandy color
            MinTemperature = 0.5f;  // Overlap with Plains for smooth transition
            MaxTemperature = 1.0f;  // Go to absolute hot
        }
        
        // Initialize sets "what this biome DOES"
        protected override void InitializeDefaultSettings()
        {
            terrainSettings = new TerrainSettings
            {
                enabled = true,
                scale = 1f,
                strength = 1f,
                baseHeight = 50f,
                baseVariation = 0f,
                generateHills = true,
                hillsHeight = 12f,
                hillsFrequency = 0.02f,
                generateMountains = false,
                surfaceBlock = Block.Types.Sand,
                subsurfaceBlock = Block.Types.Sand,
                deepBlock = Block.Types.Stone,
                surfaceDepth = 1,
                subsurfaceDepth = 4
            };

            featureSettings[typeof(TerrainSettings)] = terrainSettings;
        }
    }
}