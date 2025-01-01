using UnityEngine;
using System.Collections.Generic;
using VoxelGame.WorldSystem.Generation.Features;

namespace VoxelGame.WorldSystem.Generation.Biomes
{
    /// <summary>
    /// Handles smooth transitions between biomes.
    /// Pure utility class - no knowledge of specific biome implementations.
    /// </summary>
    public static class BiomeBlending
    {
        /// <summary>
        /// Get blended feature settings for a given position
        /// </summary>
        public static T GetBlendedSettings<T>(float temperature, float blendRange = 0.1f) where T : FeatureSettings, new()
        {
            var biomeWeights = GetBiomeWeights(temperature, blendRange);
            if (biomeWeights.Count == 0)
                return new T();

            // If only one biome influences this point, return its settings directly
            if (biomeWeights.Count == 1)
                return biomeWeights[0].biome.GetFeatureSettings<T>();

            // Create new settings instance to store blended values
            var blendedSettings = new T();
            float totalWeight = 0f;

            // Blend all numeric fields based on biome weights
            foreach (var (biome, weight) in biomeWeights)
            {
                var biomeSettings = biome.GetFeatureSettings<T>();
                BlendSettings(blendedSettings, biomeSettings, weight);
                totalWeight += weight;
            }

            // Normalize blended values
            if (totalWeight > 0)
                NormalizeSettings(blendedSettings, totalWeight);

            return blendedSettings;
        }

        /// <summary>
        /// Get all biomes that influence a point and their weights
        /// </summary>
        private static List<(BiomeBase biome, float weight)> GetBiomeWeights(float temperature, float blendRange)
        {
            return BiomeRegistry.GetBiomesForTemperature(temperature);
        }

        /// <summary>
        /// Blend settings from source into target based on weight
        /// </summary>
        private static void BlendSettings<T>(T target, T source, float weight) where T : FeatureSettings
        {
            var fields = typeof(T).GetFields();
            foreach (var field in fields)
            {
                if (field.FieldType == typeof(float))
                {
                    float sourceValue = (float)field.GetValue(source);
                    float currentValue = (float)field.GetValue(target);
                    field.SetValue(target, currentValue + sourceValue * weight);
                }
                else if (field.FieldType == typeof(int))
                {
                    int sourceValue = (int)field.GetValue(source);
                    int currentValue = (int)field.GetValue(target);
                    field.SetValue(target, currentValue + (int)(sourceValue * weight));
                }
                else if (field.FieldType == typeof(bool))
                {
                    // For booleans, use highest weight as deciding factor
                    bool sourceValue = (bool)field.GetValue(source);
                    bool currentValue = (bool)field.GetValue(target);
                    if (sourceValue && weight > 0.5f)
                        field.SetValue(target, true);
                }
            }
        }

        /// <summary>
        /// Normalize blended settings by dividing by total weight
        /// </summary>
        private static void NormalizeSettings<T>(T settings, float totalWeight) where T : FeatureSettings
        {
            var fields = typeof(T).GetFields();
            foreach (var field in fields)
            {
                if (field.FieldType == typeof(float))
                {
                    float value = (float)field.GetValue(settings);
                    field.SetValue(settings, value / totalWeight);
                }
                else if (field.FieldType == typeof(int))
                {
                    int value = (int)field.GetValue(settings);
                    field.SetValue(settings, Mathf.RoundToInt(value / totalWeight));
                }
            }
        }
    }
}