using UnityEngine;
using System.Collections.Generic;
using VoxelGame.WorldSystem.Generation.Features;

namespace VoxelGame.WorldSystem.Generation.Biomes
{
    /// <summary>
    /// Base class for biome configuration.
    /// Defines how a biome modifies and enables/disables features.
    /// </summary>
    public abstract class BiomeBase
    {
        public BiomeType Type { get; protected set; }
        public string Name { get; protected set; }
        public Color Color { get; protected set; }

        // Temperature range for this biome (0-1)
        public float MinTemperature { get; protected set; }
        public float MaxTemperature { get; protected set; }

        // Base terrain settings for this biome
        protected TerrainSettings terrainSettings;

        // Dictionary of feature settings overrides
        protected Dictionary<System.Type, FeatureSettings> featureSettings;

        protected BiomeBase()
        {
            featureSettings = new Dictionary<System.Type, FeatureSettings>();
        }

        // Add this method to be called after constructor
        public void Initialize()
        {
            InitializeDefaultSettings();
        }

        /// <summary>
        /// Initialize default settings for this biome
        /// </summary>
        protected virtual void InitializeDefaultSettings()
        {
            terrainSettings = new TerrainSettings
            {
                enabled = true,
                scale = 1f,
                strength = 1f,
                baseHeight = 64f,
                baseVariation = 3f,
                generateHills = true,
                hillsFrequency = 0.02f,
                hillsHeight = 12f,
                hillsScale = 0.8f,
                generateMountains = false,
                surfaceDepth = 1,
                subsurfaceDepth = 4
            };

            // Store terrain settings in feature settings dictionary
            featureSettings[typeof(TerrainSettings)] = terrainSettings;
        }

        /// <summary>
        /// Get settings for a specific feature type
        /// </summary>
        public T GetFeatureSettings<T>() where T : FeatureSettings, new()
        {
            var featureType = typeof(T);
            if (featureSettings.TryGetValue(featureType, out var settings))
            {
                return settings as T;
            }
            return new T();
        }

        /// <summary>
        /// Check if this biome is valid for the given temperature
        /// </summary>
        public bool IsValidForTemperature(float temperature)
        {
            return temperature >= MinTemperature && temperature <= MaxTemperature;
        }

        /// <summary>
        /// Get the weight of this biome for the given temperature (for blending)
        /// </summary>
        public float GetWeightForTemperature(float temperature)
        {
            if (!IsValidForTemperature(temperature))
                return 0f;

            // Calculate how "central" this temperature is for this biome
            float center = (MinTemperature + MaxTemperature) * 0.5f;
            float range = (MaxTemperature - MinTemperature) * 0.5f;
            float distance = Mathf.Abs(temperature - center);
            
            // Return 1 at center, 0 at edges
            return 1f - (distance / range);
        }
    }
} 