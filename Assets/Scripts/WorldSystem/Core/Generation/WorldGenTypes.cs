using UnityEngine;
using WorldSystem.Data;

namespace WorldSystem.Generation
{
    [System.Serializable]
    public struct NoiseSettings
    {
        [Tooltip("Size of the noise pattern (larger values = smaller features)")]
        public float Scale;

        [Tooltip("Height/strength of the noise")]
        public float Amplitude;

        [Tooltip("How frequently the pattern repeats")]
        public float Frequency;

        [Tooltip("Number of noise layers (more = more detail)")]
        public int Octaves;

        [Tooltip("How much each octave contributes")]
        public float Persistence;

        [Tooltip("How much detail increases per octave")]
        public float Lacunarity;

        [Tooltip("Random seed for consistent generation")]
        public int Seed;
    }

    [System.Serializable]
    public struct BiomeBlockLayer
    {
        [Tooltip("Type of block to use in this layer")]
        public BlockType BlockType;

        [Tooltip("Starting depth below surface")]
        public float MinDepth;

        [Tooltip("Ending depth below surface")]
        public float MaxDepth;

        [Tooltip("Optional noise settings for non-uniform layer generation")]
        public NoiseSettings LayerNoise;
    }

    [System.Serializable]
    public struct BiomeHeightSettings
    {
        [Tooltip("Base terrain height for this biome")]
        public float BaseHeight;

        [Tooltip("How much the height can vary up/down")]
        public float HeightVariation;

        [Tooltip("Noise settings that control the terrain shape")]
        public NoiseSettings TerrainNoiseSettings;

        [Tooltip("Offset from sea level (positive = higher, negative = lower)")]
        public float SeaLevelOffset;
    }

    [System.Serializable]
    public struct TerrainDensitySettings
    {
        [Header("3D Terrain Density")]
        [Tooltip("Base density value (-1 = always air, 0 = neutral, 1 = always solid)")]
        [Range(-1f, 1f)]
        public float DensityBias;

        [Tooltip("Overall strength of the height effect")]
        [Range(0f, 1f)]
        public float HeightScale;

        [Tooltip("Linear rate of density change with height (higher = faster falloff)")]
        [Range(0f, 2f)]
        public float LinearScale;

        [Tooltip("Exponential curve of density change (1 = linear, >1 = more exponential)")]
        [Range(0f, 15f)]
        public float VerticalBias;

        [Tooltip("Y-level where vertical density changes begin")]
        [Range(0f, 256f)]
        public float GradientStartHeight;

        [Tooltip("Fine-tune the final density")]
        [Range(-1f, 1f)]
        public float HeightOffset;
    }

    [System.Serializable]
    public struct BiomeSettings
    {
        [Header("Basic Biome Settings")]
        [Tooltip("Unique identifier for this biome")]
        public int BiomeId;

        [Tooltip("Temperature value (0-1) affects biome placement")]
        public float Temperature;

        [Tooltip("Humidity value (0-1) affects biome placement")]
        public float Humidity;

        [Tooltip("Preferred continentalness value (0-1: ocean to inland)")]
        public float Continentalness;

        [Header("Height Settings")]
        [Tooltip("Settings that control the terrain height and shape")]
        public BiomeHeightSettings HeightSettings;

        [Header("3D Terrain Generation")]
        [Tooltip("Settings for 3D terrain density")]
        public TerrainDensitySettings DensitySettings;

        [Header("Layer Settings")]
        [Tooltip("First ground layer (usually top soil)")]
        public BiomeBlockLayer Layer1;

        [Tooltip("Second ground layer (usually stone/rock)")]
        public BiomeBlockLayer Layer2;

        [Tooltip("Third ground layer (optional, for special features)")]
        public BiomeBlockLayer Layer3;

        [Tooltip("Number of active layers (1-3)")]
        public int LayerCount;

        [Header("Surface Settings")]
        [Tooltip("Block type used on the surface")]
        public BlockType DefaultSurfaceBlock;

        [Tooltip("Block type used for underwater surface")]
        public BlockType UnderwaterSurfaceBlock;

        [Tooltip("Depth below sea level to switch to underwater block")]
        public float UnderwaterThreshold;
    }

    public struct ClimateParameters
    {
        public float Temperature;
        public float Humidity;
        public float Continentalness;
    }
} 