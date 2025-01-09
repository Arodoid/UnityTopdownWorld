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
        [Tooltip("Base density value (negative = air, positive = solid)")]
        [Range(-200f, 200f)]
        public float DensityBias;

        [Tooltip("How quickly density changes with height")]
        [Range(0.000001f, 100f)]
        public float HeightScale;

        [Tooltip("Vertical offset for density transition")]
        [Range(-300f, 300f)]
        public float HeightOffset;

        [Tooltip("Controls how sharply the vertical gradient changes (higher = sharper)")]
        [Range(0.1f, 10f)]
        public float VerticalBias;

        [Tooltip("Height at which the vertical gradient starts to take effect")]
        [Range(0f, 256f)]
        public float GradientStartHeight;
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