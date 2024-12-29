using UnityEngine;

namespace VoxelGame.Entities.Definitions.Static
{
    [CreateAssetMenu(fileName = "TreeConfig", menuName = "VoxelGame/Entity/Static/Tree Configuration")]
    public class TreeConfig : ScriptableObject
    {
        [System.Serializable]
        public class TreeVariationConfig
        {
            public string treeName;
            public GameObject prefab;
            public float growthRate;
            public float maxHeight;
        }

        public TreeVariationConfig[] treeVariations;
    }
} 