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
            MinTemperature = 0.3f;
            MaxTemperature = 0.7f;
        }
        
        // Initialize sets "what this biome DOES"
        protected override void InitializeDefaultSettings()
        {
            base.InitializeDefaultSettings();

            terrainSettings.baseHeight = 64f;
            terrainSettings.baseVariation = 0f;
            terrainSettings.generateHills = true;
            terrainSettings.hillsHeight = 12f;
            terrainSettings.hillsFrequency = 0.02f;
            terrainSettings.generateMountains = false;

            // Set block types
            terrainSettings.surfaceBlock = Block.Types.Grass;
            terrainSettings.subsurfaceBlock = Block.Types.Dirt;
            terrainSettings.deepBlock = Block.Types.Stone;

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
            MinTemperature = 0.7f;  // Hotter than plains
            MaxTemperature = 1.0f;
        }
        
        // Initialize sets "what this biome DOES"
        protected override void InitializeDefaultSettings()
        {
            base.InitializeDefaultSettings();

            terrainSettings.baseHeight = 62f;
            terrainSettings.baseVariation = 2f;      // Slight variation for dunes
            terrainSettings.generateHills = true;
            terrainSettings.hillsHeight = 8f;        // Sand dunes
            terrainSettings.hillsFrequency = 0.05f;  // Spread out dunes
            terrainSettings.generateMountains = false;

            // Set block types
            terrainSettings.surfaceBlock = Block.Types.Sand;
            terrainSettings.subsurfaceBlock = Block.Types.Sand;
            terrainSettings.deepBlock = Block.Types.Sandstone;

            featureSettings[typeof(TerrainSettings)] = terrainSettings;
        }
    }
}