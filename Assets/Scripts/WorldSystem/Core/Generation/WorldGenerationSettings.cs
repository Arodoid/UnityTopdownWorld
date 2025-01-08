using UnityEngine;

namespace WorldSystem.Generation
{
    [CreateAssetMenu(fileName = "WorldGenerationSettings", menuName = "World/Generation Settings")]
    public class WorldGenerationSettings : ScriptableObject
    {
        [Header("World Seed")]
        public int seed = 123;

        [Header("Noise Scales")]
        [Range(0.0001f, 0.01f)]
        public float continentScale = 0.002f;
        [Range(0.0001f, 0.01f)]
        public float temperatureScale = 0.003f;
        [Range(0.0001f, 0.01f)]
        public float moistureScale = 0.0025f;
        [Range(0.0001f, 0.01f)]
        public float weirdnessScale = 0.004f;
        [Range(0.0001f, 0.01f)]
        public float erosionScale = 0.003f;
        [Range(0.0001f, 0.1f)]
        public float localVariationScale = 0.01f;

        [Header("Height Settings")]
        [Range(0, 255)]
        public int waterLevel = 64;
        [Range(0, 255)]
        public int oceanFloorMin = 20;
        [Range(0, 255)]
        public int oceanFloorMax = 30;
        [Range(0, 255)]
        public int mountainHeight = 100;

        [Header("Biome Thresholds")]
        [Range(0f, 1f)]
        public float oceanThreshold = 0.4f;
        [Range(0f, 1f)]
        public float mountainThreshold = 0.7f;
        [Range(0f, 1f)]
        public float forestThreshold = 0.6f;

        [Header("Variation Settings")]
        [Range(0f, 50f)]
        public float localVariationStrength = 15f;
        [Range(0f, 50f)]
        public float mountainVariationStrength = 20f;
        [Range(0f, 20f)]
        public float forestVariationStrength = 8f;
        [Range(0f, 20f)]
        public float plainsVariationStrength = 5f;

        [Header("Erosion Settings")]
        [Range(0f, 1f)]
        public float erosionStrength = 0.5f;
        [Range(0f, 1f)]
        public float erosionDetailInfluence = 0.3f;
    }
} 