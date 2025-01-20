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

        public PathfindingUtility Pathfinding => _pathfinding;

        private void Awake()
        {
            Debug.Log("EntityManager Awake");
            
            _registry = GetComponentInChildren<EntityRegistry>();
            if (_registry == null)
            {
                var registryObj = new GameObject("EntityRegistry");
                registryObj.transform.SetParent(transform);
                _registry = registryObj.AddComponent<EntityRegistry>();
            }
            
            Debug.Log("Creating TickSystem");
            _tickSystem = gameObject.AddComponent<TickSystem>();
            
            CreateEntityContainers();
        }

        public void Initialize(WorldSystemAPI worldAPI)
        {
            _pathfinding = new PathfindingUtility(worldAPI);
            Debug.Log("EntityManager initialized with WorldSystemAPI");
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

        public EntityHandle CreateEntity(string entityId, int3 position)
        {
            // Convert block position to world position (center of block)
            var worldPosition = new int3(
                position.x,
                position.y,
                position.z
            );
                        
            // Get next available ID
            int id = GetNextEntityId();
            _currentVersion++;

            // Create entity GameObject
            var entityObject = new GameObject($"{entityId}_{id}");
            entityObject.transform.SetParent(transform);
            
            // Create and initialize entity component with correct type
            var entity = entityObject.AddComponent<Entity>();
            
            // Determine entity type based on template
            EntityType entityType = DetermineEntityType(entityId);
            entity.Initialize(id, _currentVersion, entityType, worldPosition);
            
            // Position the GameObject at block center
            entityObject.transform.position = new Vector3(
                worldPosition.x + 0.5f,  // Center of block
                worldPosition.y,
                worldPosition.z + 0.5f   // Center of block
            );
            
            // Add visual component by default
            entity.AddComponent<EntityVisualComponent>();

            // Apply template if it exists
            if (_registry.TryGetTemplate(entityId, out var setup))
            {
                setup(entity);
            }
            
            // Store entity
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
            Debug.Log($"Creating entity of type {type} at position {position}");
            
            // Simulate async initialization if needed
            await Task.Yield();
            
            // Get next available ID
            int entityId = GetNextEntityId();
            _currentVersion++;

            // Create entity GameObject
            var entityObject = new GameObject($"{type}_{entityId}");
            entityObject.transform.SetParent(_entityContainers[type]);
            
            // Create and initialize entity component
            var entity = entityObject.AddComponent<Entity>();
            entity.Initialize(entityId, _currentVersion, type, position);
            
            // Store entity
            _entities[entityId] = entity;
            
            var handle = new EntityHandle(entityId, _currentVersion);
            Debug.Log($"Created entity with handle: {handle.Id}, {handle.Version}");
            return handle;
        }

        public bool DestroyEntity(EntityHandle handle)
        {
            if (TryGetEntity(handle, out Entity entity))
            {
                Debug.Log($"Destroying entity {handle.Id}");
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
                position = entity.Position;
                return true;
            }
            return false;
        }

        public bool SetEntityPosition(EntityHandle handle, int3 position)
        {
            if (TryGetEntity(handle, out Entity entity))
            {
                entity.Position = position;
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
    }
}