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
        [SerializeField] private Vector3 _debugCubePosition = new Vector3(0f, 65f, 0f);

        private JobSystemComponent _jobSystem;
        private TickSystem _tickSystem;
        
        private EntityRegistry _entityRegistry;
        private Dictionary<int, Entity> _entities = new();
        private Dictionary<EntityType, Transform> _entityContainers = new();
        private HashSet<int> _recycledIds = new();
        private int _nextEntityId = 1;
        private uint _nextVersion = 1;
        private PathfindingUtility _pathfinding;
        private WorldSystemAPI _worldAPI;

        public PathfindingUtility Pathfinding => _pathfinding;
        public WorldSystemAPI WorldAPI => _worldAPI;
        public TickSystem TickSystem => _tickSystem;

        private void Awake()
        {
            Debug.Log("EntityManager Awake");
            
            _jobSystem = GetComponent<JobSystemComponent>();
            _tickSystem = GetComponent<TickSystem>();
            
            if (_jobSystem == null || _tickSystem == null)
            {
                Debug.LogError("Required components JobSystemComponent or TickSystem not found on EntityManager GameObject!");
            }
            
            _entityRegistry = new EntityRegistry();
            _entityRegistry.SetSystems(_jobSystem, _tickSystem);
            
            CreateEntityContainers();
        }

        public void Initialize(WorldSystemAPI worldAPI)
        {
            _worldAPI = worldAPI;
            _pathfinding = new PathfindingUtility(worldAPI, this);
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

        public EntityHandle CreateEntity(string templateName, int3 position)
        {
            Debug.Log($"Creating entity at block position {position}");
            
            if (!_entityRegistry.TryGetTemplate(templateName, out var template))
            {
                Debug.LogError($"No template found for '{templateName}'");
                return EntityHandle.Invalid;
            }
            
            int entityId = GetNextEntityId();
            uint version = _nextVersion++;
            
            var entityObject = new GameObject($"{templateName}_{entityId}");
            entityObject.transform.SetParent(_entityContainers[template.Type]);
            
            // Position is the block position - entity lives in the center of that block
            entityObject.transform.position = new Vector3(
                position.x + 0.5f,
                position.y,           // Changed: Y is whole number
                position.z + 0.5f
            );
            
            var entity = entityObject.AddComponent<Entity>();
            entity.Initialize(entityId, version, template.Type, this);
            
            template.Setup(entity);
            _entities[entityId] = entity;
            
            return new EntityHandle(entityId, version);
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
            if (_entities.TryGetValue(handle.Id, out var entity))
            {
                Vector3 worldPos = entity.transform.position;
                position = new int3(
                    Mathf.FloorToInt(worldPos.x),
                    Mathf.FloorToInt(worldPos.y) + 1,  // Changed: Floor + 1 instead of Ceil
                    Mathf.FloorToInt(worldPos.z)
                );
                return true;
            }
            return false;
        }

        public bool SetEntityPosition(EntityHandle handle, int3 position)
        {
            if (_entities.TryGetValue(handle.Id, out var entity))
            {
                entity.transform.position = new Vector3(
                    position.x + 0.5f,
                    position.y,           // Changed: Y is whole number
                    position.z + 0.5f
                );
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
            return _entityRegistry.GetAvailableTemplates();
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

        public void Update()
        {
            // Process path requests each frame
            _pathfinding.ProcessPathRequests();
        }

        // Update all other position methods to match:
        internal bool TryGetEntityPosition(int entityId, out int3 position)
        {
            position = default;
            if (_entities.TryGetValue(entityId, out var entity))
            {
                Vector3 worldPos = entity.transform.position;
                position = new int3(
                    Mathf.FloorToInt(worldPos.x),
                    Mathf.FloorToInt(worldPos.y) + 1,  // Changed: Floor + 1 instead of Ceil
                    Mathf.FloorToInt(worldPos.z)
                );
                return true;
            }
            return false;
        }

        internal bool SetEntityPosition(int entityId, int3 position)
        {
            if (_entities.TryGetValue(entityId, out var entity))
            {
                entity.transform.position = new Vector3(
                    position.x + 0.5f,
                    position.y - 1f,      // Changed: -1f instead of -0.5f to lower by 0.5 more
                    position.z + 0.5f
                );
                return true;
            }
            return false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(_debugCubePosition, Vector3.one);
        }

        // World Space → Block Space
        public int3 GetBlockPosition(Vector3 worldPosition)
        {
            return new int3(
                Mathf.FloorToInt(worldPosition.x),
                Mathf.FloorToInt(worldPosition.y) + 1,  // Changed: Floor + 1 instead of Ceil
                Mathf.FloorToInt(worldPosition.z)
            );
        }
    }
}//