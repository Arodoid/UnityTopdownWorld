using UnityEngine;
using System;
using VoxelGame.Extensions;
using VoxelGame.Entities;

namespace VoxelGame.Entities.Definitions.NPC
{
    public abstract class NPCEntity : MonoBehaviour, IEntity
    {
        [SerializeField] protected NPCConfig npcConfig;
        
        protected Guid entityId;
        protected Vector3Int targetPosition;
        protected float moveTimer;
        protected float pathUpdateTimer;
        protected bool isMoving;

        public virtual void Initialize(EntityPrefabConfig.EntityPrefabEntry config, Guid id)
        {
            entityId = id;
            moveTimer = 0f;
            pathUpdateTimer = 0f;
            isMoving = false;
        }

        protected virtual void Update()
        {
            if (!isMoving) return;
            
            moveTimer += Time.deltaTime;
            pathUpdateTimer += Time.deltaTime;

            // Update path periodically
            if (pathUpdateTimer >= npcConfig.npcVariations[0].updateRate)
            {
                pathUpdateTimer = 0f;
                UpdatePathfinding();
            }

            // Move towards target
            if (moveTimer >= 1f / npcConfig.npcVariations[0].moveSpeed)
            {
                moveTimer = 0f;
                MoveTowardsTarget();
            }
        }

        protected virtual void UpdatePathfinding()
        {
            // Override in specific NPC types
            // This is where you'd implement your pathfinding logic
        }

        protected virtual void MoveTowardsTarget()
        {
            Vector3Int currentPos = transform.position.ToVector3Int();
            
            if (Vector3Int.Distance(currentPos, targetPosition) <= 1f)
            {
                isMoving = false;
                return;
            }

            // Convert to Vector3 for direction calculation
            Vector3 direction = ((Vector3)(targetPosition - currentPos)).normalized;
            Vector3Int moveDirection = direction.DirectionToVector3Int();
            Vector3Int newPos = currentPos + moveDirection;

            // Move the entity
            EntityDataManager.Instance.MoveEntity(entityId, newPos);
        }

        protected bool CanMoveTo(Vector3Int position)
        {
            // This is where you'd check the world data
            // Example implementation:
            return true; // Placeholder
        }

        public void SetTarget(Vector3Int newTarget)
        {
            targetPosition = newTarget;
            isMoving = true;
        }
    }
} 