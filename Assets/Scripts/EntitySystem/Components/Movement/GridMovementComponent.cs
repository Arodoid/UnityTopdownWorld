using Unity.Mathematics;
using UnityEngine;
using EntitySystem.Core;
using EntitySystem.Core.World;

namespace EntitySystem.Components.Movement
{
    public class GridMovementComponent : EntityComponent
    {
        private IWorldAccess _worldAccess;
        public float MoveSpeed { get; set; } = 3f;
        public float FallSpeed { get; set; } = 20f;
        
        private Vector3 _velocity;
        public Vector3 Velocity 
        { 
            get => _velocity;
            private set => _velocity = value;
        }
        
        public bool IsGrounded { get; private set; }
        
        public override void Initialize(Entity entity)
        {
            base.Initialize(entity);
            _worldAccess = Entity.Manager.GetWorldAccess();
        }

        public override void OnTick()
        {
            // Split movement into separate phases
            // 1. First handle falling/vertical movement
            HandleVerticalMovement();
            
            // 2. Then handle horizontal movement only if grounded
            if (IsGrounded)
            {
                HandleHorizontalMovement();
            }
            else
            {
                Debug.LogWarning($"Entity {Entity.Id} not moving: not grounded");
            }
        }

        private void HandleVerticalMovement()
        {
            // Check if we're grounded
            IsGrounded = CheckGround();
            
            if (!IsGrounded)
            {
                // Apply gravity
                _velocity.y -= FallSpeed * TickManager.TICK_INTERVAL;
                
                // Apply vertical movement
                Vector3 nextPos = Entity.Position + new Vector3(0, _velocity.y * TickManager.TICK_INTERVAL, 0);
                
                // Debug visualization
                Debug.DrawLine(Entity.Position, nextPos, Color.red, TickManager.TICK_INTERVAL);
                
                Entity.Position = nextPos;
                Entity.GameObject.transform.position = Entity.Position;
            }
            else
            {
                // Stop falling when grounded
                _velocity.y = 0;
            }
        }

        private void HandleHorizontalMovement()
        {
            Vector3 horizontalVelocity = new Vector3(_velocity.x, 0, _velocity.z);
            
            if (horizontalVelocity.magnitude < 0.01f)
                return;
                
            Vector3 nextPos = Entity.Position + horizontalVelocity * TickManager.TICK_INTERVAL;
            
            // Debug visualization
            Debug.DrawLine(Entity.Position, nextPos, Color.green, TickManager.TICK_INTERVAL);
            
            // Check if we can move there (has ground support and no obstacles)
            int3 targetPos = WorldToBlockPos(nextPos);
            if (_worldAccess.CanStandAt(targetPos))
            {
                Entity.Position = nextPos;
                Entity.GameObject.transform.position = Entity.Position;
            }
            else
            {
                Debug.LogWarning($"Entity {Entity.Id} blocked at target position {targetPos}");
                // Stop horizontal movement
                _velocity.x = 0;
                _velocity.z = 0;
            }
        }

        private float FindGroundLevel(Vector3 position)
        {
            int3 pos = WorldToBlockPos(position);
            for (int y = pos.y; y >= 0; y--)
            {
                if (_worldAccess.IsBlockSolid(new int3(pos.x, y, pos.z)))
                {
                    return y + 1; // Return the position above the solid block
                }
            }
            return 0;
        }

        public void SetVelocity(Vector3 velocity)
        {
            // Preserve vertical velocity (for falling)
            float oldY = _velocity.y;
            _velocity = velocity;
            _velocity.y = oldY;
        }

        private bool CheckGround()
        {
            Vector3 checkPos = Entity.Position + Vector3.down * 0.1f; // Check slightly below current position
            int3 blockPos = WorldToBlockPos(checkPos);
            
            bool isSolid = _worldAccess.IsBlockSolid(blockPos);
            
            // Debug visualization
            Color debugColor = isSolid ? Color.green : Color.red;
            Debug.DrawRay(Entity.Position, Vector3.down * 0.2f, debugColor, TickManager.TICK_INTERVAL);
            
            return isSolid;
        }

        private bool CanMoveTo(Vector3 worldPos)
        {
            int3 pos = WorldToBlockPos(worldPos);
            return _worldAccess.CanStandAt(pos);
        }

        private static int3 WorldToBlockPos(Vector3 worldPos)
        {
            return new int3(
                Mathf.FloorToInt(worldPos.x),
                Mathf.FloorToInt(worldPos.y),
                Mathf.FloorToInt(worldPos.z)
            );
        }
    }
} 