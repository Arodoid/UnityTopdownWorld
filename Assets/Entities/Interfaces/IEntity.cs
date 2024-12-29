using System;
using VoxelGame.Entities.Definitions;

namespace VoxelGame.Entities
{
    public interface IEntity
    {
        void Initialize(EntityPrefabConfig.EntityPrefabEntry config, Guid entityId);
    }
} 