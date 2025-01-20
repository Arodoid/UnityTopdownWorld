using UnityEngine;
using Unity.Mathematics;
using EntitySystem.Core.Utilities;

namespace EntitySystem.Core.Components
{
    public class IdleMovementComponent : EntityComponent, ITickable
    {
        [SerializeField] public float _idleMovementRange = 5f;
        [SerializeField] public float _minIdleTime = 0.3f;
        [SerializeField] public float _maxIdleTime = 1f;
        [SerializeField] public float _entityHeight = 1f;

        private MovementComponent _movement;
        private PathfindingUtility _pathfinding;
        private float _idleTimer;
        private EntityManager _entityManager;

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
            if (_movement.IsMoving)
            {
                return;
            }

            _idleTimer -= Time.fixedDeltaTime;
            
            if (_idleTimer <= 0)
            {
                TryRandomMovement();
                ResetIdleTimer();
            }
        }

        private void TryRandomMovement()
        {
            var path = _pathfinding.FindRandomPath(
                Entity.Position, 
                _idleMovementRange,
                _entityHeight
            );
            
            if (path.Count > 0)
            {
                _movement.MoveTo(path);
            }
        }

        private void ResetIdleTimer()
        {
            _idleTimer = UnityEngine.Random.Range(_minIdleTime, _maxIdleTime);
        }
    }
} 