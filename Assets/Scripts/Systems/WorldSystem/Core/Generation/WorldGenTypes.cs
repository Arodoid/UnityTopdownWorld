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
    }

    public struct ClimateParameters
    {
        public float Temperature;
        public float Humidity;
        public float Continentalness;
    }
}