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
    public struct TerrainDensitySettings
    {
        [Header("Height Zones")]
        public float DeepStart;      // Start of deep to cave transition
        public float CaveStart;      // End of deep to cave transition
        public float CaveEnd;        // Start of cave to surface transition
        public float SurfaceStart;   // End of cave to surface transition
        public float SurfaceEnd;     // Start of surface to air transition

        [Header("Bias Settings")]
        public float DeepBias;       // Constant bias for deep zone
        public float CaveBias;       // Constant bias for caves
        public float SurfaceBias;    // Constant bias for surface

        [Header("Transition Scales")]
        public float DeepTransitionScale;    // Deep to cave transition
        public float CaveTransitionScale;    // Cave to surface transition
        public float AirTransitionScale;     // Surface to air transition

        [Header("Transition Curves")]
        [Tooltip("Power/curve for deep transition (1 = linear, >1 = slow start, <1 = fast start)")]
        public float DeepTransitionCurve;
        [Tooltip("Power/curve for cave-to-surface transition")]
        public float CaveTransitionCurve;
        [Tooltip("Power/curve for surface-to-air transition")]
        public float AirTransitionCurve;
    }

    [System.Serializable]
    public struct BiomeSettings
    {
        [Header("Basic Biome Settings")]
        public int BiomeId;
        public float Temperature;
        public float Humidity;
        public float Continentalness;

        [Header("3D Terrain Generation")]
        public TerrainDensitySettings DensitySettings;

        [Header("Block Types")]
        public BlockType PrimaryBlock;      // Main block type (like dirt)
        public BlockType SecondaryBlock;    // Deep/underground block type (like stone)
        public BlockType TopBlock;          // Block when exposed to air (like grass)
        public BlockType UnderwaterBlock;   // Block when underwater and exposed
        public float UnderwaterThreshold;   // How deep before switching to underwater block

        [Header("Vegetation")]
        public float TreeDensity;      // Chance of tree per surface block (0-1)
        public float TreeMinHeight;    // Minimum tree height
        public float TreeMaxHeight;    // Maximum tree height
        public bool AllowsTrees;       // Whether this biome can have trees

        [Header("Rock Features")]
        public float RockDensity;        // Chance of rock per surface block (0-1)
        public float RockMinSize;        // Minimum rock radius
        public float RockMaxSize;        // Maximum rock radius
        public float RockSpikiness;      // How jagged/rough the rocks are (0-1)
        public float RockGroundDepth;    // How deep rocks embed into ground
        public bool AllowsRocks;         // Whether this biome can have rocks
    }

    public struct ClimateParameters
    {
        public float Temperature;
        public float Humidity;
        public float Continentalness;
    }
} 