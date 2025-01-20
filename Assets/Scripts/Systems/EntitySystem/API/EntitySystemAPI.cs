using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;
using EntitySystem.Data;

namespace EntitySystem.API
{
    public class EntitySystemAPI
    {
        private readonly IEntityManager _entityManager;

        public EntitySystemAPI(IEntityManager entityManager)
        {
            _entityManager = entityManager;
        }

        public EntityHandle CreateEntity(string entityId, int3 position)
        {
            return _entityManager.CreateEntity(entityId, position);
        }

        public bool DestroyEntity(EntityHandle handle)
        {
            return _entityManager.DestroyEntity(handle);
        }

        public bool TryGetEntityPosition(EntityHandle handle, out int3 position)
        {
            return _entityManager.TryGetEntityPosition(handle, out position);
        }

        public bool SetEntityPosition(EntityHandle handle, int3 position)
        {
            return _entityManager.SetEntityPosition(handle, position);
        }

        public IEnumerable<string> GetEntityTypes()
        {
            return System.Enum.GetNames(typeof(EntityType));
        }

        public IEnumerable<string> GetAvailableEntityTemplates()
        {
            return _entityManager.GetAvailableTemplates();
        }
    }
} 