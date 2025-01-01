using UnityEngine;
using System.Collections.Generic;

namespace VoxelGame.WorldSystem.Generation.Biomes
{
    /// <summary>
    /// Central registry for all biomes.
    /// Handles biome registration, lookup, and temperature-based biome selection.
    /// No knowledge of feature implementation - just manages biome instances.
    /// </summary>
    public static class BiomeRegistry
    {
        private static readonly Dictionary<BiomeType, BiomeBase> biomes = new();
        private static bool initialized = false;

        public static void Initialize()
        {
            if (initialized) return;

            // Register all biomes
            RegisterBiome(new PlainsBiome());
            RegisterBiome(new DesertBiome());
            // RegisterBiome(new MountainBiome());
            // RegisterBiome(new ForestBiome());
            // RegisterBiome(new TundraBiome());

            initialized = true;
        }

        private static void RegisterBiome(BiomeBase biome)
        {
            biomes[biome.Type] = biome;
        }

        public static BiomeBase GetBiome(BiomeType type)
        {
            if (!initialized) Initialize();
            return biomes.TryGetValue(type, out var biome) ? biome : biomes[BiomeType.Plains];
        }

        public static Color GetBiomeColor(BiomeType type)
        {
            return GetBiome(type).Color;
        }

        public static BiomeBase[] GetAllBiomes()
        {
            if (!initialized) Initialize();
            var biomeArray = new BiomeBase[biomes.Count];
            biomes.Values.CopyTo(biomeArray, 0);
            return biomeArray;
        }

        /// <summary>
        /// Get the most appropriate biome for a given temperature
        /// </summary>
        public static BiomeType GetBiomeType(float temperature)
        {
            if (!initialized) Initialize();

            BiomeType bestBiome = BiomeType.Plains;
            float bestWeight = float.MinValue;

            foreach (var biome in biomes.Values)
            {
                float weight = biome.GetWeightForTemperature(temperature);
                if (weight > bestWeight)
                {
                    bestWeight = weight;
                    bestBiome = biome.Type;
                }
            }

            return bestBiome;
        }

        /// <summary>
        /// Get all biomes that could influence a given temperature, with their weights
        /// Used for biome blending
        /// </summary>
        public static List<(BiomeBase biome, float weight)> GetBiomesForTemperature(float temperature)
        {
            if (!initialized) Initialize();

            var result = new List<(BiomeBase, float)>();
            
            foreach (var biome in biomes.Values)
            {
                float weight = biome.GetWeightForTemperature(temperature);
                if (weight > 0)
                {
                    result.Add((biome, weight));
                }
            }

            return result;
        }
    }
} 