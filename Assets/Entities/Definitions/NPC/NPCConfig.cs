using UnityEngine;

namespace VoxelGame.Entities.Definitions.NPC
{
    [CreateAssetMenu(fileName = "NPCConfig", menuName = "VoxelGame/Entity/NPC Configuration")]
    public class NPCConfig : ScriptableObject
    {
        [System.Serializable]
        public class NPCVariationConfig
        {
            public string npcName;
            public GameObject prefab;
            public float moveSpeed = 1f;
            public float updateRate = 0.5f;
            public int viewDistance = 10;
        }

        public NPCVariationConfig[] npcVariations;
    }
} 