using UnityEngine;

namespace VoxelGame.WorldSystem.Biomes
{
    [CreateAssetMenu(fileName = "BiomeAttributes", menuName = "VoxelGame/Biome Attributes")]
    public class BiomeAttributes : ScriptableObject
    {
        [Header("Biome Identification")]
        public BiomeType biomeType;
        public string biomeName;

        [Header("Temperature Settings")]
        public float minTemperature = 0f;
        public float maxTemperature = 1f;

        [Header("Height Modification")]
        public float heightMultiplier = 1f;
        public float heightOffset = 0f;

        [Header("Surface Settings")]
        [SerializeField] private string surfaceBlockName = "Grass";    // References Block.Types
        [SerializeField] private string subsurfaceBlockName = "Dirt";  // References Block.Types
        public int surfaceDepth = 1;
        public int subsurfaceDepth = 4;

        // Properties to get the actual blocks
        public Block SurfaceBlock => GetBlockByName(surfaceBlockName);
        public Block SubsurfaceBlock => GetBlockByName(subsurfaceBlockName);

        private Block GetBlockByName(string name)
        {
            // This will use reflection to get the block from Block.Types
            var blockType = typeof(Block.Types).GetProperty(name);
            return blockType?.GetValue(null) as Block;
        }

        private void OnValidate()
        {
            // Ensure block names are valid
            if (string.IsNullOrEmpty(surfaceBlockName))
                surfaceBlockName = "Grass";
            if (string.IsNullOrEmpty(subsurfaceBlockName))
                subsurfaceBlockName = "Dirt";
        }
    }
} 