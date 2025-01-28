using UnityEngine;
using WorldSystem.Data;

namespace WorldSystem.Generation
{
    [System.Serializable]
    public struct BiomeBlockLayer
    {
        [Tooltip("Type of block to use in this layer")]
        public BlockType BlockType;
        [Tooltip("Starting depth below surface")]
        public float MinDepth;
        [Tooltip("Ending depth below surface")]
        public float MaxDepth;
    }

    [System.Serializable]
    public struct BiomeSettings
    {
        [Header("Climate Parameters")]
        [Tooltip("0-1, cold to hot")]
        public float PreferredTemperature;
        [Tooltip("0-1, dry to wet")]
        public float PreferredHumidity;
        [Tooltip("0-1, low to high elevation")]
        public float PreferredContinentalness;

        [Header("Block Types")]
        public BlockType TopBlock;          // Main surface block
        public BlockType UnderwaterBlock;   // Block when below water level

        [Header("Tree Settings")]
        public bool AllowsTrees;
        public float TreeDensity;
        public float TreeMinHeight;
        public float TreeMaxHeight;
        public bool IsPalmTree;            // Whether trees in this biome are palm trees

        [Header("Rock Settings")]
        public bool AllowsRocks;
        public float RockDensity;
        public float RockMinSize;
        public float RockMaxSize;
        public float RockSpikiness;
        public float RockGroundDepth;
    }

    public struct ClimateParameters
    {
        public float Temperature;
        public float Humidity;
        public float Continentalness;
    }
}