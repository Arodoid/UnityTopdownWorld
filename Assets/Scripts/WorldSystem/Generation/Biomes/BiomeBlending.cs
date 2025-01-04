using UnityEngine;
using System.Collections.Generic;
using VoxelGame.WorldSystem.Generation.Features;
using System.Linq;

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

            // Normalize weights first
            float totalWeight = biomeWeights.Sum(bw => bw.weight);
            var normalizedWeights = biomeWeights.Select(bw => 
                (bw.biome, weight: bw.weight / totalWeight)).ToList();

            // Create new settings instance
            var blendedSettings = new T();

            // Get all fields that need blending
            var fields = typeof(T).GetFields();
            foreach (var field in fields)
            {
                if (field.FieldType == typeof(float))
                {
                    // Blend float values
                    float blendedValue = 0f;
                    foreach (var (biome, weight) in normalizedWeights)
                    {
                        var biomeSettings = biome.GetFeatureSettings<T>();
                        float value = (float)field.GetValue(biomeSettings);
                        blendedValue += value * weight;
                    }
                    field.SetValue(blendedSettings, blendedValue);
                }
                else if (field.FieldType == typeof(int))
                {
                    // Blend integer values
                    float blendedValue = 0f;
                    foreach (var (biome, weight) in normalizedWeights)
                    {
                        var biomeSettings = biome.GetFeatureSettings<T>();
                        int value = (int)field.GetValue(biomeSettings);
                        blendedValue += value * weight;
                    }
                    field.SetValue(blendedSettings, Mathf.RoundToInt(blendedValue));
                }
                else if (field.FieldType == typeof(bool))
                {
                    // Use weighted majority for boolean values
                    float trueWeight = 0f;
                    foreach (var (biome, weight) in normalizedWeights)
                    {
                        var biomeSettings = biome.GetFeatureSettings<T>();
                        bool value = (bool)field.GetValue(biomeSettings);
                        if (value) trueWeight += weight;
                    }
                    field.SetValue(blendedSettings, trueWeight > 0.5f);
                }
                else if (field.FieldType == typeof(Block))
                {
                    // Use highest weight for block types
                    var dominantBiome = normalizedWeights.OrderByDescending(bw => bw.weight).First();
                    var dominantSettings = dominantBiome.biome.GetFeatureSettings<T>();
                    field.SetValue(blendedSettings, field.GetValue(dominantSettings));
                }
            }

            return blendedSettings;
        }

        /// <summary>
        /// Get all biomes that influence a point and their weights
        /// </summary>
        private static List<(BiomeBase biome, float weight)> GetBiomeWeights(float temperature, float blendRange)
        {
            var weights = BiomeRegistry.GetBiomesForTemperature(temperature);
            
            // Ensure weights are reasonable
            if (weights.Count > 0)
            {
                float totalWeight = weights.Sum(w => w.weight);
                if (totalWeight <= 0)
                {
                    // If no valid weights, use nearest biome
                    var nearestBiome = BiomeRegistry.GetAllBiomes()
                        .OrderBy(b => Mathf.Abs(temperature - (b.MinTemperature + b.MaxTemperature) * 0.5f))
                        .First();
                    return new List<(BiomeBase, float)> { (nearestBiome, 1f) };
                }
            }
            
            return weights;
        }
    }
}