using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;
using System.Threading.Tasks;
using EntitySystem.API;
using EntitySystem.Data;
using EntitySystem.Core.Components;
using EntitySystem.Core.Utilities;  // Just need this for PathfindingUtility
using WorldSystem.API;

namespace EntitySystem.Core
{
    public class EntityManager : MonoBehaviour, IEntityManager
    {
        private EntityRegistry _registry;
        private Dictionary<int, Entity> _entities = new();
        private Dictionary<EntityType, Transform> _entityContainers = new();
        private HashSet<int> _recycledIds = new();
        private int _nextEntityId;
        private uint _currentVersion;
        private TickSystem _tickSystem;
        private PathfindingUtility _pathfinding;
        private WorldSystemAPI _worldAPI;

        public PathfindingUtility Pathfinding => _pathfinding;

        private void Awake()
        {
            Debug.Log("EntityManager Awake");
            
            // Get or create registry
            _registry = GetComponentInChildren<EntityRegistry>();
            if (_registry == null)
            {
                var registryObj = new GameObject("EntityRegistry");
                registryObj.transform.SetParent(transform);
                _registry = registryObj.AddComponent<EntityRegistry>();
            }
            
            // Only get existing TickSystem, don't create a new one
            _tickSystem = FindAnyObjectByType<TickSystem>();
            
            CreateEntityContainers();
        }

        public void Initialize(WorldSystemAPI worldAPI)
        {
            _pathfinding = new PathfindingUtility(worldAPI);
            _worldAPI = worldAPI;
        }

        private void CreateEntityContainers()
        {
            var systemContainer = new GameObject("EntityContainers").transform;
            systemContainer.SetParent(transform);
            
            foreach (EntityType type in System.Enum.GetValues(typeof(EntityType)))
            {
                var container = new GameObject($"{type}Container").transform;
                container.SetParent(systemContainer);
                _entityContainers[type] = container;
            }
        }

        public EntityHandle CreateEntity(string entityId, int3 blockPosition)
        {
            Debug.Log($"Creating entity at block position {blockPosition}");
            
            // Find the first air block above this position
            while (_worldAPI.IsBlockSolid(blockPosition))
            {
                blockPosition.y += 1;
            }
            
            // Get next available ID
            int id = GetNextEntityId();
            _currentVersion++;

            // Create entity GameObject
            var entityObject = new GameObject($"{entityId}_{id}");
            entityObject.transform.SetParent(transform);
            
            // Create and initialize entity component with correct type
            var entity = entityObject.AddComponent<Entity>();
            EntityType entityType = DetermineEntityType(entityId);
            
            // Pass 'this' as the EntityManager
            entity.Initialize(id, _currentVersion, entityType, this);
            
            // Convert block position to world position (centered in block)
            entityObject.transform.position = new Vector3(
                blockPosition.x + 0.5f,  // Center in block X
                blockPosition.y,         // Bottom of block Y
                blockPosition.z + 0.5f   // Center in block Z
            );
            
            // Add visual component by default
            entity.AddComponent<EntityVisualComponent>();

            if (_registry.TryGetTemplate(entityId, out var setup))
            {
                setup(entity);
            }
            
            _entities[id] = entity;
            
            return new EntityHandle(id, _currentVersion);
        }

        private EntityType DetermineEntityType(string entityId)
        {
            // You could make this more sophisticated, but for now:
            return entityId switch
            {
                "Dog" or "Colonist" => EntityType.Living,
                "WoodenChair" => EntityType.Furniture,
                _ => EntityType.Item  // Default case
            };
        }

        public async Task<EntityHandle> CreateEntity(EntityType type, int3 position)
        {
            await Task.Yield();
            
            int entityId = GetNextEntityId();
            _currentVersion++;

            var entityObject = new GameObject($"{type}_{entityId}");
            entityObject.transform.SetParent(_entityContainers[type]);
            
            var entity = entityObject.AddComponent<Entity>();
            // Pass 'this' as the EntityManager
            entity.Initialize(entityId, _currentVersion, type, this);
            
            // Set position directly on transform
            entityObject.transform.position = new Vector3(position.x, position.y, position.z);
            
            _entities[entityId] = entity;
            
            return new EntityHandle(entityId, _currentVersion);
        }

        public bool DestroyEntity(EntityHandle handle)
        {
            if (TryGetEntity(handle, out Entity entity))
            {
                _entities.Remove(handle.Id);
                _recycledIds.Add(handle.Id);
                Destroy(entity.gameObject);
                return true;
            }
            Debug.LogWarning($"Failed to destroy entity: invalid handle {handle.Id}");
            return false;
        }

        public bool TryGetEntityPosition(EntityHandle handle, out int3 position)
        {
            position = default;
            if (TryGetEntity(handle, out Entity entity))
            {
                Vector3 worldPos = entity.transform.position;
                position = new int3((int)worldPos.x, (int)worldPos.y, (int)worldPos.z);
                return true;
            }
            return false;
        }

        public bool SetEntityPosition(EntityHandle handle, int3 position)
        {
            if (TryGetEntity(handle, out Entity entity))
            {
                entity.transform.position = new Vector3(position.x, position.y, position.z);
                return true;
            }
            return false;
        }

        private bool TryGetEntity(EntityHandle handle, out Entity entity)
        {
            if (_entities.TryGetValue(handle.Id, out entity))
            {
                if (entity.Version == handle.Version)
                {
                    return true;
                }
                Debug.LogWarning($"Entity version mismatch. Handle: {handle.Version}, Entity: {entity.Version}");
            }
            entity = null;
            return false;
        }

        private int GetNextEntityId()
        {
            if (_recycledIds.Count > 0)
            {
                using var enumerator = _recycledIds.GetEnumerator();
                enumerator.MoveNext();
                int recycledId = enumerator.Current;
                _recycledIds.Remove(recycledId);
                return recycledId;
            }
            return _nextEntityId++;
        }

        public T AddComponent<T>(EntityHandle handle) where T : Component, IEntityComponent
        {
            if (TryGetEntity(handle, out Entity entity))
            {
                return entity.AddComponent<T>();
            }
            return null;
        }

        public T GetComponent<T>(EntityHandle handle) where T : class, IEntityComponent
        {
            if (TryGetEntity(handle, out Entity entity))
            {
                return entity.GetComponent<T>();
            }
            return null;
        }

        public IEnumerable<string> GetAvailableTemplates()
        {
            return _registry.GetAvailableTemplates();
        }

        public IEnumerable<EntityHandle> GetEntitiesInRadius(Vector3 position, float radius)
        {
            float sqrRadius = radius * radius;
            foreach (var entity in _entities.Values)
            {
                float sqrDistance = (entity.transform.position - position).sqrMagnitude;
                if (sqrDistance <= sqrRadius)
                {
                    yield return new EntityHandle(entity.Id, entity.Version);
                }
            }
        }

        public Vector3? GetEntityPosition(EntityHandle handle)
        {
            if (TryGetEntity(handle, out Entity entity))
            {
                return entity.transform.position;
            }
            return null;
        }
    }
}