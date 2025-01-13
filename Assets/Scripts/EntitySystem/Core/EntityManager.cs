using UnityEngine;
using System;
using System.Collections.Generic;
using Unity.Mathematics;
using EntitySystem.Core.World;

namespace EntitySystem.Core
{
    public class EntityManager : MonoBehaviour
    {
        private Dictionary<long, Entity> _entities = new();
        private HashSet<Entity> _tickEntities = new();
        private long _nextEntityId = 0;
        
        [SerializeField] private TickManager tickManager;
        private IWorldAccess _worldAccess;

        private void Start()
        {
            if (tickManager == null)
                tickManager = GetComponent<TickManager>();
                
            tickManager.OnTick += HandleTick;
        }

        public void Initialize(IWorldAccess worldAccess)
        {
            _worldAccess = worldAccess;
        }

        public IWorldAccess GetWorldAccess()
        {
            return _worldAccess;
        }

        public Entity CreateEntity(Vector3 position)
        {
            var entity = new Entity(_nextEntityId++, this);
            _entities[entity.Id] = entity;
            
            // Create GameObject
            var go = new GameObject($"Entity_{entity.Id}");
            go.transform.position = position;
            entity.Initialize(go);
            
            return entity;
        }

        public void DestroyEntity(Entity entity)
        {
            if (_entities.Remove(entity.Id))
            {
                entity.OnDestroy();
            }
        }

        private void HandleTick()
        {
            foreach (var entity in _tickEntities)
            {
                if (entity.IsActive)
                    entity.OnTick();
            }
        }

        public void RegisterForTicks(Entity entity)
        {
            _tickEntities.Add(entity);
        }

        public void UnregisterFromTicks(Entity entity)
        {
            _tickEntities.Remove(entity);
        }

        public T CreateEntity<T>(Vector3 position) where T : Entity
        {
            // Create the specific entity type
            var entity = (T)Activator.CreateInstance(typeof(T), _nextEntityId++, this);
            _entities[entity.Id] = entity;
            
            // Create GameObject
            var go = new GameObject($"Entity_{entity.Id}");
            go.transform.position = position;
            entity.Initialize(go);
            
            return entity;
        }
    }
} 