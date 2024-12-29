using UnityEngine;
using System;

namespace VoxelGame.Entities.Definitions.Static
{
    public abstract class StaticEntity : MonoBehaviour, IEntity
    {
        protected Guid entityId;

        public virtual void Initialize(EntityPrefabConfig.EntityPrefabEntry config, Guid id)
        {
            entityId = id;
        }
    }
} 