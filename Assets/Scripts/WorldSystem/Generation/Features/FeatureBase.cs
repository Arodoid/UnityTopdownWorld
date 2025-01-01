using UnityEngine;
using VoxelGame.WorldSystem.Generation.Core;
using VoxelGame.WorldSystem.Generation.Biomes;

namespace VoxelGame.WorldSystem.Generation.Features
{
    /// <summary>
    /// Base interface for all world generation features.
    /// Each feature is a self-contained module that can modify the world
    /// based on its own settings and the provided noise generator.
    /// </summary>
    public interface IWorldFeature
    {
        /// <summary>
        /// Apply this feature to a chunk at the given position
        /// </summary>
        void Apply(Chunk chunk, NoiseGenerator noise);
        
        /// <summary>
        /// Whether this feature is enabled for the given biome
        /// </summary>
        bool IsEnabledForBiome(BiomeType biomeType);
    }

    /// <summary>
    /// Base settings class that all feature settings will inherit from
    /// </summary>
    public class FeatureSettings
    {
        public bool enabled = true;
        public float scale = 1f;
        public float strength = 1f;
    }

    /// <summary>
    /// Base class for all world generation features
    /// Provides common functionality and enforces consistent implementation
    /// </summary>
    public abstract class WorldFeature : IWorldFeature
    {
        protected FeatureSettings settings;

        protected WorldFeature(FeatureSettings settings)
        {
            this.settings = settings;
        }

        public abstract void Apply(Chunk chunk, NoiseGenerator noise);

        public virtual bool IsEnabledForBiome(BiomeType biomeType)
        {
            return settings.enabled;
        }

        /// <summary>
        /// Helper method to get noise value with feature's scale applied
        /// </summary>
        protected float GetFeatureNoise(NoiseGenerator noise, float x, float z, int offsetIndex)
        {
            return noise.GetNoise(x, z, offsetIndex, settings.scale);
        }
    }
} 