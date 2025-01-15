using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using EntitySystem.Core.Interfaces;
using EntitySystem.Core.Types;
using EntitySystem.Core.World;
using EntitySystem.Core.Jobs;

namespace EntitySystem.Core
{
    public class EntityManager : MonoBehaviour
    {
        private readonly Dictionary<long, IEntity> _entities = new();
        private readonly HashSet<IEntity> _activeEntities = new();
        private readonly Dictionary<System.Type, Queue<IEntity>> _entityPools = new();
        private long _nextEntityId;
        
        [SerializeField] private int initialPoolSize = 100;
        [SerializeField] private TickManager tickManager;
        private DirectWorldAccess _worldAccess;
        public bool IsInitialized { get; private set; }
        [SerializeField] private JobSystem jobSystem;

        private void Start()
        {
            if (tickManager == null)
                tickManager = GetComponent<TickManager>();
                
            tickManager.OnTick += HandleTick;
        }

        public void PreloadEntityPool<T>(int count) where T : IEntity
        {
            var pool = GetOrCreatePool<T>();
            for (int i = 0; i < count; i++)
            {
                var entity = CreateEntityInternal(Vector3.zero, typeof(T));
                if (entity != null)
                {
                    entity.SetState(EntityState.Pooled);
                    pool.Enqueue(entity);
                }
            }
        }

        private Queue<IEntity> GetOrCreatePool<T>() where T : IEntity
        {
            var type = typeof(T);
            if (!_entityPools.ContainsKey(type))
            {
                _entityPools[type] = new Queue<IEntity>();
            }
            return _entityPools[type];
        }

        public void Initialize(DirectWorldAccess worldAccess)
        {
            _worldAccess = worldAccess;
            IsInitialized = true;
        }

        public DirectWorldAccess GetWorldAccess()
        {
            return _worldAccess;
        }

        public T CreateEntity<T>(Vector3 position) where T : IEntity
        {
            var pool = GetOrCreatePool<T>();
            if (pool.Count > 0)
            {
                var pooledEntity = pool.Dequeue();
                if (pooledEntity is T typedEntity)
                {
                    pooledEntity.GameObject.transform.position = position;
                    pooledEntity.SetState(EntityState.Active);
                    return typedEntity;
                }
                pool.Enqueue(pooledEntity);
            }
            
            return CreateNewEntity<T>(position);
        }

        private T CreateNewEntity<T>(Vector3 position) where T : IEntity
        {
            var entity = CreateEntityInternal(position, typeof(T));
            
            // Ensure the created entity is of type T
            if (entity is T typedEntity)
            {
                return typedEntity;
            }
            
            throw new InvalidCastException(
                $"Created entity of type {entity.GetType().Name} could not be cast to {typeof(T).Name}"
            );
        }

        private Entity CreateEntityInternal(Vector3 position, System.Type entityType)
        {
            try
            {
                var entity = (Entity)System.Activator.CreateInstance(
                    entityType, 
                    new object[] { _nextEntityId++, this }
                );
                
                _entities[entity.Id] = entity;
                
                var go = new GameObject($"Entity_{entity.Id}");
                go.transform.SetParent(this.transform);
                go.transform.position = position;
                entity.Initialize(go);
                
                entity.SetState(EntityState.Active);
                _activeEntities.Add(entity);
                
                return entity;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to create entity of type {entityType.Name}: {e}");
                throw;
            }
        }

        public void DestroyEntity(IEntity entity)
        {
            if (_entities.Remove(entity.Id))
            {
                _activeEntities.Remove(entity);
                
                var entityType = entity.GetType();
                if (!_entityPools.ContainsKey(entityType))
                {
                    _entityPools[entityType] = new Queue<IEntity>();
                }
                
                entity.SetState(EntityState.Pooled);
                _entityPools[entityType].Enqueue(entity);
            }
        }

        private void HandleTick()
        {
            foreach (var entity in _activeEntities)
            {
                if (entity.IsActive)
                {
                    entity.OnTick();
                }
            }
        }

        public void RegisterForTicks(IEntity entity)
        {
            if (entity.IsActive)
                _activeEntities.Add(entity);
        }

        public void UnregisterFromTicks(IEntity entity)
        {
            _activeEntities.Remove(entity);
        }

        private void OnDestroy()
        {
            foreach (var entity in _entities.Values)
            {
                entity.OnDestroy();
            }
            
            _entities.Clear();
            _activeEntities.Clear();
            _entityPools.Clear();
        }

        public IEntity GetEntity(long id)
        {
            return _entities.TryGetValue(id, out var entity) ? entity : null;
        }

        public IEnumerable<IEntity> GetEntitiesInRadius(Vector3 position, float radius)
        {
            float sqrRadius = radius * radius;
            return _activeEntities.Where(e => 
                (e.GameObject.transform.position - position).sqrMagnitude <= sqrRadius);
        }

        public JobSystem GetJobSystem() => jobSystem;

        public Entity GetEntitiesInCell(int x, int z)
        {
            // Find first entity in this cell
            foreach (var entityPair in _entities)
            {
                var entity = entityPair.Value as Entity;
                if (entity == null) continue;
                
                var transform = entity.GameObject.transform;
                Vector3 pos = transform.position;
                int entityX = Mathf.RoundToInt(pos.x);
                int entityZ = Mathf.RoundToInt(pos.z);
                
                if (entityX == x && entityZ == z)
                {
                    return entity;
                }
            }
            return null;
        }
    }
}