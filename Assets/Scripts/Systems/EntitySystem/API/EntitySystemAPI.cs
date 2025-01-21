using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;
using EntitySystem.Core;
using EntitySystem.Data;

namespace EntitySystem.API
{
    public class EntitySystemAPI
    {
        private readonly EntityManager _entityManager;
        private readonly JobSystemComponent _jobSystem;

        public EntitySystemAPI(EntityManager entityManager, JobSystemComponent jobSystem)
        {
            if (entityManager == null)
                Debug.LogError("EntitySystemAPI: EntityManager is null!");
            if (jobSystem == null)
                Debug.LogError("EntitySystemAPI: JobSystemComponent is null!");

            _entityManager = entityManager;
            _jobSystem = jobSystem;
            
            Debug.Log($"EntitySystemAPI initialized with EntityManager: {entityManager != null}, JobSystem: {jobSystem != null}");
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

        public void AddGlobalJob(IJob job)
        {
            if (_jobSystem == null)
            {
                Debug.LogError("Cannot add job: JobSystemComponent is null!");
                return;
            }
            
            if (job == null)
            {
                Debug.LogError("Cannot add job: Job is null!");
                return;
            }

            _jobSystem.AddGlobalJob(job);
            Debug.Log($"Added global job of type {job.GetType().Name}");
        }
    }
} 