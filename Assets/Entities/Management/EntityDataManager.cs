using UnityEngine;
using System;
using System.Collections.Generic;
using VoxelGame.Core.Debugging;

namespace VoxelGame.Entities
{
    /// <summary>
    /// Manages all entity data in the game world. Handles creation, deletion, and queries.
    /// This class is independent of Unity's GameObject system and focuses on data management.
    /// </summary>
    public class EntityDataManager
    {
        private static EntityDataManager instance;
        public static EntityDataManager Instance => instance ??= new EntityDataManager();

        // Core data storage
        private readonly Dictionary<Guid, EntityData> entities = new();
        private readonly Dictionary<Vector3Int, HashSet<Guid>> spatialMap = new();

        // Events for system communication
        public event Action<EntityData> OnEntityCreated;
        public event Action<EntityData> OnEntityRemoved;
        public event Action<EntityData, Vector3Int> OnEntityMoved;

        private EntityDataManager() { }

        public EntityData CreateEntity(EntityType type, Vector3Int position, Quaternion rotation)
        {
            var entityData = new EntityData(type, position, rotation);
            
            entities[entityData.UUID] = entityData;
            
            if (!spatialMap.ContainsKey(position))
                spatialMap[position] = new HashSet<Guid>();
            spatialMap[position].Add(entityData.UUID);

            OnEntityCreated?.Invoke(entityData);
            return entityData;
        }

        public void RemoveEntity(Guid uuid)
        {
            if (!entities.TryGetValue(uuid, out EntityData entityData))
                return;

            if (spatialMap.ContainsKey(entityData.Position))
                spatialMap[entityData.Position].Remove(uuid);

            entities.Remove(uuid);
            OnEntityRemoved?.Invoke(entityData);
        }

        public void MoveEntity(Guid uuid, Vector3Int newPosition)
        {
            if (!entities.TryGetValue(uuid, out EntityData entityData))
                return;

            var oldPosition = entityData.Position;
            
            // Update spatial mapping
            if (spatialMap.ContainsKey(oldPosition))
                spatialMap[oldPosition].Remove(uuid);
            
            if (!spatialMap.ContainsKey(newPosition))
                spatialMap[newPosition] = new HashSet<Guid>();
            spatialMap[newPosition].Add(uuid);

            entityData.UpdatePosition(newPosition);
            OnEntityMoved?.Invoke(entityData, oldPosition);
        }

        public EntityData GetEntity(Guid uuid)
        {
            return entities.TryGetValue(uuid, out EntityData entityData) ? entityData : null;
        }

        public IEnumerable<EntityData> GetEntitiesInArea(Vector3Int centerChunk, int chunkRadius)
        {
            var result = new HashSet<EntityData>();
            
            // Convert chunk radius to world units
            int worldRadius = chunkRadius * Chunk.ChunkSize;
            
            // Calculate bounds in world space
            Vector3Int minBound = new Vector3Int(
                (centerChunk.x - chunkRadius) * Chunk.ChunkSize,
                0,
                (centerChunk.z - chunkRadius) * Chunk.ChunkSize
            );
            
            Vector3Int maxBound = new Vector3Int(
                (centerChunk.x + chunkRadius) * Chunk.ChunkSize,
                WorldDataManager.WORLD_HEIGHT_IN_CHUNKS * Chunk.ChunkSize,
                (centerChunk.z + chunkRadius) * Chunk.ChunkSize
            );

            // Check all positions within bounds
            for (int x = minBound.x; x <= maxBound.x; x++)
            for (int z = minBound.z; z <= maxBound.z; z++)
            for (int y = minBound.y; y < maxBound.y; y++)
            {
                var checkPos = new Vector3Int(x, y, z);
                if (spatialMap.TryGetValue(checkPos, out var entitiesAtPos))
                {
                    foreach (var uuid in entitiesAtPos)
                    {
                        if (entities.TryGetValue(uuid, out var entity))
                            result.Add(entity);
                    }
                }
            }

            return result;
        }
    }
}