using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System;

namespace VoxelGame.Entities.Definitions.Static
{
    public abstract class TreeEntity : StaticEntity
    {
        [SerializeField] protected TreeConfig treeConfig;
        
        public override void Initialize(EntityPrefabConfig.EntityPrefabEntry config, Guid id)
        {
            base.Initialize(config, id);
            ApplyTreeSpecificBehavior();
        }
        
        protected virtual void ApplyTreeSpecificBehavior()
        {
            // Common tree behavior
        }
    }
}