using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using EntitySystem.Core.Interfaces;
using EntitySystem.Core.Types;
using WorldSystem;
using EntitySystem.Access;

namespace EntitySystem.Core
{
    public class EntityManager : MonoBehaviour, IEntitySystem
    {
        [Header("Required Systems")]
        [SerializeField] private TickManager tickManager;
        [SerializeField] private JobSystem.Core.JobSystem jobSystem;
        
        // Simplified collections
        private readonly Dictionary<Type, HashSet<IEntity>> _entitySets = new();
        private readonly Dictionary<long, IEntity> _entityLookup = new();
        private readonly HashSet<IEntity> _activeEntities = new();
        private long _nextEntityId;
        private IWorldSystem _worldSystem;
        
        private readonly Dictionary<Vector2Int, HashSet<IEntity>> _gridCells = new();
        private const float CELL_SIZE = 1f; // Assuming 1 unit grid cells
        
        public bool IsInitialized { get; private set; }

        private void Start()
        {
            ValidateRequiredSystems();
            tickManager.OnTick += HandleTick;
        }

        private void ValidateRequiredSystems()
        {
            tickManager ??= GetComponent<TickManager>();
            jobSystem ??= GetComponent<JobSystem.Core.JobSystem>();
            
            if (tickManager == null)
                Debug.LogError($"[{gameObject.name}] TickManager reference missing!");
            if (jobSystem == null)
                Debug.LogError($"[{gameObject.name}] JobSystem reference missing!");
        }

        public void Initialize(IWorldSystem worldSystem, JobSystem.Core.JobSystem jobSys = null)
        {
            _worldSystem = worldSystem;
            if (jobSys != null) jobSystem = jobSys;
            
            if (jobSystem == null)
            {
                Debug.LogWarning($"[{gameObject.name}] No JobSystem provided, attempting to find one...");
                jobSystem = FindFirstObjectByType<JobSystem.Core.JobSystem>();
                
                if (jobSystem == null)
                    Debug.LogError($"[{gameObject.name}] Failed to find JobSystem in scene!");
            }
            
            IsInitialized = true;
        }

        public T CreateEntity<T>(Vector3 position) where T : Entity
        {
            var entityType = typeof(T);
            var entity = GetInactiveEntity<T>() ?? CreateNewEntity<T>(position);
            
            if (entity != null)
            {
                entity.GameObject.transform.position = position;
                ActivateEntity(entity);
            }
            
            return entity;
        }

        private T GetInactiveEntity<T>() where T : Entity
        {
            if (!_entitySets.TryGetValue(typeof(T), out var entities)) return null;
            return entities.FirstOrDefault(e => e.State == EntityState.Pooled) as T;
        }

        private T CreateNewEntity<T>(Vector3 position) where T : Entity
        {
            try
            {
                var entity = (T)Activator.CreateInstance(typeof(T), new object[] { _nextEntityId++, this });
                var entityType = typeof(T);
                
                if (!_entitySets.TryGetValue(entityType, out var entities))
                {
                    entities = new HashSet<IEntity>();
                    _entitySets[entityType] = entities;
                }
                
                entities.Add(entity);
                _entityLookup[entity.Id] = entity;
                
                var go = new GameObject($"{entityType.Name}_{entity.Id}");
                go.transform.SetParent(transform);
                go.transform.position = position;
                entity.Initialize(go);
                
                return entity;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to create entity of type {typeof(T).Name}: {e}");
                throw;
            }
        }

        private void ActivateEntity(IEntity entity)
        {
            entity.SetState(EntityState.Active);
            _activeEntities.Add(entity);
            
            // Add to grid
            var cell = WorldToGrid(entity.Position);
            if (!_gridCells.TryGetValue(cell, out var entities))
            {
                entities = new HashSet<IEntity>();
                _gridCells[cell] = entities;
            }
            entities.Add(entity);
        }

        public void DeactivateEntity(IEntity entity)
        {
            _activeEntities.Remove(entity);
            
            // Remove from grid
            var cell = WorldToGrid(entity.Position);
            if (_gridCells.TryGetValue(cell, out var entities))
            {
                entities.Remove(entity);
                if (entities.Count == 0)
                    _gridCells.Remove(cell);
            }
            
            entity.SetState(EntityState.Pooled);
        }

        public void DestroyEntity(IEntity entity)
        {
            var entityType = entity.GetType();
            if (_entitySets.TryGetValue(entityType, out var entities))
            {
                entities.Remove(entity);
                _entityLookup.Remove(entity.Id);
                _activeEntities.Remove(entity);
                entity.OnDestroy();
            }
        }

        private void HandleTick()
        {
            foreach (var entity in _activeEntities)
            {
                if (entity.State == EntityState.Active)
                {
                    var oldPos = entity.Position;
                    entity.OnTick();
                    
                    // Update grid position if needed
                    if (oldPos != entity.Position)
                    {
                        UpdateEntityGridPosition(entity, oldPos, entity.Position);
                    }
                }
            }
        }

        public IEnumerable<T> GetEntitiesOfType<T>() where T : IEntity
        {
            if (_entitySets.TryGetValue(typeof(T), out var entities))
            {
                return entities.Cast<T>();
            }
            return Enumerable.Empty<T>();
        }

        public IEnumerable<IEntity> GetEntitiesInRadius(Vector3 position, float radius)
        {
            float sqrRadius = radius * radius;
            return _activeEntities.Where(e => 
                (e.GameObject.transform.position - position).sqrMagnitude <= sqrRadius);
        }

        public IWorldSystem GetWorldSystem() => _worldSystem;
        public JobSystem.Core.JobSystem GetJobSystem() => jobSystem;

        private void OnDestroy()
        {
            foreach (var entities in _entitySets.Values)
            {
                foreach (var entity in entities)
                {
                    entity.OnDestroy();
                }
            }
            
            _entitySets.Clear();
            _entityLookup.Clear();
            _activeEntities.Clear();
        }

        private void UpdateEntityGridPosition(IEntity entity, Vector3 oldPosition, Vector3 newPosition)
        {
            var oldCell = WorldToGrid(oldPosition);
            var newCell = WorldToGrid(newPosition);
            
            if (oldCell != newCell)
            {
                // Remove from old cell
                if (_gridCells.TryGetValue(oldCell, out var oldEntities))
                {
                    oldEntities.Remove(entity);
                    if (oldEntities.Count == 0)
                        _gridCells.Remove(oldCell);
                }
                
                // Add to new cell
                if (!_gridCells.TryGetValue(newCell, out var newEntities))
                {
                    newEntities = new HashSet<IEntity>();
                    _gridCells[newCell] = newEntities;
                }
                newEntities.Add(entity);
            }
        }

        private Vector2Int WorldToGrid(Vector3 worldPosition)
        {
            return new Vector2Int(
                Mathf.RoundToInt(worldPosition.x / CELL_SIZE),
                Mathf.RoundToInt(worldPosition.z / CELL_SIZE)
            );
        }

        public IEnumerable<IEntity> GetEntitiesInCell(Vector2Int cellPosition)
        {
            return _gridCells.TryGetValue(cellPosition, out var entities) 
                ? entities 
                : Enumerable.Empty<IEntity>();
        }

        public void AddToActiveEntities(IEntity entity)
        {
            _activeEntities.Add(entity);
        }
        
        public void RemoveFromActiveEntities(IEntity entity)
        {
            _activeEntities.Remove(entity);
        }

        // New interface implementations
        public long CreateEntity(string entityType, Vector3 position)
        {
            // Convert string type to actual Type and use existing method
            var type = Type.GetType(entityType);
            var entity = CreateEntity<Entity>(position); // Need to modify this
            return entity.Id;
        }

        public void DestroyEntity(long entityId)
        {
            if (_entityLookup.TryGetValue(entityId, out var entity))
            {
                DestroyEntity(entity);
            }
        }

        public bool HasComponent(long entityId, string componentType)
        {
            if (_entityLookup.TryGetValue(entityId, out var entity))
            {
                var type = Type.GetType(componentType);
                return entity.GetComponents().Any(c => c.GetType() == type);
            }
            return false;
        }

        public void SetComponentValue(long entityId, string componentType, object value)
        {
            if (_entityLookup.TryGetValue(entityId, out var entity))
            {
                var type = Type.GetType(componentType);
                var component = entity.GetComponents().FirstOrDefault(c => c.GetType() == type);
                if (component != null)
                {
                    // If the component has a Value property, set it
                    var valueProperty = component.GetType().GetProperty("Value");
                    if (valueProperty != null)
                    {
                        valueProperty.SetValue(component, value);
                    }
                    else
                    {
                        Debug.LogError($"Component {componentType} does not have a Value property");
                    }
                }
            }
        }

        public T GetComponentValue<T>(long entityId, string componentType)
        {
            if (_entityLookup.TryGetValue(entityId, out var entity))
            {
                var type = Type.GetType(componentType);
                var component = entity.GetComponents().FirstOrDefault(c => c.GetType() == type);
                if (component != null)
                {
                    // If the component has a Value property, get it
                    var valueProperty = component.GetType().GetProperty("Value");
                    if (valueProperty != null)
                    {
                        return (T)valueProperty.GetValue(component);
                    }
                    else
                    {
                        Debug.LogError($"Component {componentType} does not have a Value property");
                    }
                }
            }
            return default(T);
        }

        public IReadOnlyList<long> GetEntitiesInRange(Vector3 position, float radius)
        {
            return GetEntitiesInRadius(position, radius)
                .Select(e => e.Id)
                .ToList()
                .AsReadOnly();
        }

        public IReadOnlyList<long> GetEntitiesWithComponent(string componentType)
        {
            var type = Type.GetType(componentType);
            return _activeEntities
                .Where(e => e.GetComponents().Any(c => c.GetType() == type))
                .Select(e => e.Id)
                .ToList()
                .AsReadOnly();
        }

        public Vector3 GetEntityPosition(long entityId)
        {
            return _entityLookup.TryGetValue(entityId, out var entity) 
                ? entity.Position 
                : Vector3.zero;
        }

        public void SetEntityState(long entityId, EntityState state)
        {
            if (_entityLookup.TryGetValue(entityId, out var entity))
            {
                entity.SetState(state);
            }
        }
    }
}