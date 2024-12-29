using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Pool;
using VoxelGame.Entities.Definitions;
using VoxelGame.Core.Debugging;

namespace VoxelGame.Entities
{
    /// <summary>
    /// Manages the GameObject representations of entities in the game world.
    /// Handles creation, destruction, and pooling of entity GameObjects based on view area.
    /// </summary>
    public class EntityObjectManager : MonoBehaviour
    {
        private static EntityObjectManager instance;
        public static EntityObjectManager Instance { get; private set; }

        [Header("References")]
        [SerializeField] private EntityPrefabConfig prefabConfig;
        [SerializeField] private Transform entityContainer;

        [Header("Pooling")]
        [SerializeField] private int defaultPoolSize = 20;

        private Dictionary<EntityType, ObjectPool<GameObject>> entityPools;
        private HashSet<Guid> activeEntities = new();
        private Dictionary<Guid, GameObject> activeGameObjects = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (prefabConfig == null)
            {
                Debug.LogError("EOM EntityPrefabConfig is not assigned! Please assign it in the Unity Inspector.");
                enabled = false;
                return;
            }

            if (entityContainer == null)
            {
                Debug.LogWarning("EOM Entity Container not assigned, creating one automatically.");
                entityContainer = new GameObject("EntityContainer").transform;
                entityContainer.SetParent(transform);
            }

            entityPools = new Dictionary<EntityType, ObjectPool<GameObject>>();
            
            // Initialize pools for each entity type
            foreach (var config in prefabConfig.entityConfigs)
            {
                InitializePool(config.type, config.prefab, defaultPoolSize);
            }

            // Subscribe to entity events
            EntityDataManager.Instance.OnEntityMoved += HandleEntityMoved;
        }

        private void HandleEntityMoved(EntityData entityData, Vector3Int oldPosition)
        {
            if (activeGameObjects.TryGetValue(entityData.UUID, out GameObject obj))
            {
                // Update GameObject position with centering offset
                obj.transform.position = entityData.Position + new Vector3(0.5f, 0.01f, 0.5f);
                Debug.Log($"Moving GameObject for entity {entityData.UUID} to {entityData.Position}");
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            if (EntityDataManager.Instance != null)
            {
                EntityDataManager.Instance.OnEntityMoved -= HandleEntityMoved;
            }
        }

        /// <summary>
        /// Updates visible entities based on a view area
        /// </summary>
        public void UpdateVisibleEntities(Vector3Int center, int radius)
        {
            var entitiesInView = EntityDataManager.Instance.GetEntitiesInArea(center, radius);

            var newVisibleSet = new HashSet<Guid>();

            // Show entities that should be visible
            foreach (var entity in entitiesInView)
            {
                newVisibleSet.Add(entity.UUID);
                if (!activeEntities.Contains(entity.UUID))
                {
                    ShowEntity(entity);
                }
            }

            // Hide entities that are no longer visible
            var toHide = new List<Guid>();
            foreach (var activeId in activeEntities)
            {
                if (!newVisibleSet.Contains(activeId))
                {
                    toHide.Add(activeId);
                }
            }

            foreach (var id in toHide)
            {
                HideEntity(id);
            }

            activeEntities = newVisibleSet;
        }

        private void ShowEntity(EntityData entityData)
        {
            Debug.Log($"EOM Attempting to show entity: Type={entityData.Type}, Position={entityData.Position}");
            
            if (entityPools.TryGetValue(entityData.Type, out var pool))
            {
                var obj = pool.Get();
                var prefabEntry = prefabConfig.GetConfigForType(entityData.Type);
                if (prefabEntry == null)
                {
                    Debug.LogError($"EOM No prefab config found for entity type: {entityData.Type}");
                    return;
                }

                // Set basic transform properties
                obj.SetActive(true);
                obj.transform.position = entityData.Position + new Vector3(0.5f, 0.01f, 0.5f);
                obj.transform.rotation = entityData.Rotation;
                obj.transform.localScale = Vector3.one * prefabEntry.scale;
                
                activeGameObjects[entityData.UUID] = obj;
                
                // Initialize any IEntity component
                var entity = obj.GetComponent<IEntity>();
                if (entity != null)
                {
                    entity.Initialize(prefabEntry, entityData.UUID);
                }
                else
                {
                    Debug.LogWarning($"EOM No IEntity component found on object of type {entityData.Type}");
                }
            }
            else
            {
                Debug.LogError($"EOM No pool found for entity type: {entityData.Type}");
            }
        }

        private void HideEntity(Guid entityId)
        {
            if (activeGameObjects.TryGetValue(entityId, out GameObject obj))
            {
                var entityData = EntityDataManager.Instance.GetEntity(entityId);
                if (entityData != null)
                {
                    if (entityPools.TryGetValue(entityData.Type, out var pool))
                    {
                        pool.Release(obj);
                        activeGameObjects.Remove(entityId);
                    }
                }
            }
        }

        private void InitializePool(EntityType type, GameObject prefab, int size)
        {
            entityPools[type] = new ObjectPool<GameObject>(
                createFunc: () => Instantiate(prefab, entityContainer),
                actionOnGet: (obj) => obj.SetActive(true),
                actionOnRelease: (obj) => obj.SetActive(false),
                actionOnDestroy: (obj) => Destroy(obj),
                defaultCapacity: size
            );
        }
    }
}