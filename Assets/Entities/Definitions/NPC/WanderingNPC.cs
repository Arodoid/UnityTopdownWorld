using UnityEngine;
using VoxelGame.Extensions;

namespace VoxelGame.Entities.Definitions.NPC
{
    public class WanderingNPC : NPCEntity
    {
        private int wanderRadius = 10;
        private float wanderTimer = 0f;
        private float wanderInterval = 5f;
        private const int HEIGHT_OFFSET = 10;

        protected override void Update()
        {
            base.Update();

            if (!isMoving)
            {
                wanderTimer += Time.deltaTime;
                if (wanderTimer >= wanderInterval)
                {
                    Debug.Log($"NPC {entityId}: Picking new target after {wanderInterval} seconds");
                    wanderTimer = 0f;
                    PickNewWanderTarget();
                }
            }
        }

        private void PickNewWanderTarget()
        {
            Vector3Int currentPos = transform.position.ToVector3Int();
            Vector3Int randomOffset = new Vector3Int(
                Random.Range(-wanderRadius, wanderRadius + 1),
                0,
                Random.Range(-wanderRadius, wanderRadius + 1)
            );

            Vector3Int newTarget = currentPos + randomOffset;
            newTarget.y = HEIGHT_OFFSET;
            Debug.Log($"NPC {entityId}: Setting new target from {currentPos} to {newTarget}");
            SetTarget(newTarget);
        }

        protected override void UpdatePathfinding()
        {
            if (!CanMoveTo(targetPosition))
            {
                PickNewWanderTarget();
            }
        }

        protected override void MoveTowardsTarget()
        {
            Vector3Int currentPos = transform.position.ToVector3Int();
            Debug.Log($"NPC {entityId}: Moving from {currentPos} towards {targetPosition}");
            base.MoveTowardsTarget();
        }
    }
} 