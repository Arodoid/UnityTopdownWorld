using UnityEngine;
using System;

namespace VoxelGame.Entities.Definitions
{
    [CreateAssetMenu(fileName = "EntityPrefabConfig", menuName = "VoxelGame/Entity/PrefabConfig")]
    public class EntityPrefabConfig : ScriptableObject
    {
        [Serializable]
        public class EntityPrefabEntry
        {
            public EntityType type;
            public GameObject prefab;
            public float scale = 1f;  // Keep basic scale only
        }

        public EntityPrefabEntry[] entityConfigs;

        public EntityPrefabEntry GetConfigForType(EntityType type)
        {
            return Array.Find(entityConfigs, config => config.type == type);
        }
    }
}