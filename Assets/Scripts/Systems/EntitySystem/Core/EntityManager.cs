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
        [SerializeField] private int3 _debugCubePosition = new int3(0, 65, 0);

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

        public EntityHandle CreateEntity(string templateName, int3 blockPosition)
        {
            Debug.Log($"Creating entity at block position {blockPosition}");
            
            if (!_entityRegistry.TryGetTemplate(templateName, out var template))
            {
                Debug.LogError($"No template found for '{templateName}'");
                return EntityHandle.Invalid;
            }
            
            int entityId = GetNextEntityId();
            uint version = _nextVersion++;
            
            var entityObject = new GameObject($"{templateName}_{entityId}");
            entityObject.transform.SetParent(_entityContainers[template.Type]);
            entityObject.transform.position = BlockToWorldSpace(blockPosition);
            
            var entity = entityObject.AddComponent<Entity>();
            entity.Initialize(entityId, version, template.Type, this);
            
            // Register entity BEFORE setting up template
            _entities[entityId] = entity;
            
            template.Setup(entity);
            
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
                position = WorldToBlockSpace(entity.transform.position);
                return true;
            }
            return false;
        }

        public bool TryGetEntityPosition(int entityId, out int3 position)
        {
            position = default;
            if (_entities.TryGetValue(entityId, out var entity))
            {
                position = WorldToBlockSpace(entity.transform.position);
                return true;
            }
            return false;
        }

        public bool SetEntityPosition(EntityHandle handle, int3 blockPosition)
        {
            if (_entities.TryGetValue(handle.Id, out var entity))
            {
                entity.transform.position = BlockToWorldSpace(blockPosition);
                return true;
            }
            return false;
        }

        public bool SetEntityPosition(int entityId, int3 blockPosition)
        {
            if (_entities.TryGetValue(entityId, out var entity))
            {
                entity.transform.position = BlockToWorldSpace(blockPosition);
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

        public void Update()
        {
            // Process path requests each frame
            _pathfinding.ProcessPathRequests();
        }

        private void OnDrawGizmosSelected()
        {
            // Convert block position to world position for visualization
            Vector3 worldPos = new Vector3(
                _debugCubePosition.x + 0.5f,
                _debugCubePosition.y,
                _debugCubePosition.z + 0.5f
            );
            
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(worldPos, Vector3.one);
        }

        // World Space → Block Space
        public int3 GetBlockPosition(Vector3 worldPosition)
        {
            return new int3(
                Mathf.FloorToInt(worldPosition.x),
                Mathf.FloorToInt(worldPosition.y),  // No more +1 magic numbers
                Mathf.FloorToInt(worldPosition.z)
            );
        }

        public IEnumerable<Entity> GetAllEntities()
        {
            return _entities.Values;
        }

        // Public conversion utilities that everyone can use
        public static int3 WorldToBlockSpace(Vector3 worldPosition)
        {
            return new int3(
                Mathf.FloorToInt(worldPosition.x),
                Mathf.FloorToInt(worldPosition.y),  // No more +1 magic numbers
                Mathf.FloorToInt(worldPosition.z)
            );
        }

        public static Vector3 BlockToWorldSpace(int3 blockPosition)
        {
            return new Vector3(
                blockPosition.x + 0.5f,  // Center in block horizontally
                blockPosition.y,         // Direct Y position
                blockPosition.z + 0.5f   // Center in block horizontally
            );
        }
    }
}//