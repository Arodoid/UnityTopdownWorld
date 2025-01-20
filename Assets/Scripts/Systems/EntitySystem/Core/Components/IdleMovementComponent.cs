using UnityEngine;
using Unity.Mathematics;
using EntitySystem.Core.Utilities;
using System.Collections;

namespace EntitySystem.Core.Components
{
    public class IdleMovementComponent : EntityComponent, ITickable
    {
        [SerializeField] public float _idleMovementRange = 5f;
        [SerializeField] public int _minIdleTicks = 3;  // Changed to ticks instead of time
        [SerializeField] public int _maxIdleTicks = 10; // Changed to ticks instead of time
        [SerializeField] public float _entityHeight = 1f;

        private MovementComponent _movement;
        private PathfindingUtility _pathfinding;
        private float _idleTimer;
        private EntityManager _entityManager;

        [SerializeField] private float _pathfindingCooldown = 0.5f;  // Minimum time between pathfinding attempts
        private float _pathfindingTimer;
        private bool _isPathfinding;

        protected override void OnInitialize(EntityManager entityManager)
        {
            _entityManager = entityManager;
            _movement = Entity.GetComponent<MovementComponent>();
            if (_movement == null)
            {
                _movement = Entity.AddComponent<MovementComponent>();
            }
            
            _pathfinding = _entityManager.Pathfinding;
            ResetIdleTimer();
            
            // Ensure initial position is centered on block
            AdjustPosition();
        }

        private void AdjustPosition()
        {
            var currentPos = Entity.Position;
            Entity.Position = new int3(
                currentPos.x,
                currentPos.y,
                currentPos.z
            );
            transform.position = new Vector3(
                currentPos.x + 0.5f,
                currentPos.y,
                currentPos.z + 0.5f
            );
        }

        public void OnTick()
        {
            
            if (_movement.IsMoving) return;

            _idleTimer -= 1;  // Decrease by 1 tick instead of deltaTime
            _pathfindingTimer -= 1;
            
            
            if (_idleTimer <= 0 && _pathfindingTimer <= 0 && !_isPathfinding)
            {
                StartCoroutine(TryRandomMovementAsync());
            }
        }

        private IEnumerator TryRandomMovementAsync()
        {
            _isPathfinding = true;
            _pathfindingTimer = _pathfindingCooldown;

            // Do the expensive random position check in a coroutine
            if (_pathfinding.TryGetRandomNearbyPosition(Entity.Position, _idleMovementRange, _entityHeight, out int3 targetPos))
            {
                // Split pathfinding into chunks
                yield return StartCoroutine(_pathfinding.FindPathAsync(
                    Entity.Position, 
                    targetPos, 
                    _entityHeight,
                    (path) => {
                        if (path != null && path.Count > 0)
                        {
                            _movement.MoveTo(path);
                        }
                        _isPathfinding = false;
                        ResetIdleTimer();
                    }
                ));
            }
            else
            {
                _isPathfinding = false;
                ResetIdleTimer();
            }
        }

        private void ResetIdleTimer()
        {
            _idleTimer = UnityEngine.Random.Range(_minIdleTicks, _maxIdleTicks + 1);
        }
    }
} 