using UnityEngine;
using Unity.Mathematics;
using EntitySystem.Core.Utilities;
using Random = UnityEngine.Random;

namespace EntitySystem.Core.Components
{
    public class IdleMovementComponent : EntityComponent, ITickable
    {
        [SerializeField] public float _idleMovementRange = 15f;
        [SerializeField] public float _entityHeight = 1f;
        [SerializeField] private float _minIdleTime = 2f;
        [SerializeField] private float _maxIdleTime = 5f;

        private MovementComponent _movement;
        private PathfindingUtility _pathfinding;
        private float _idleTimer;
        private bool _isWaitingForPath;

        protected override void OnInitialize(EntityManager entityManager)
        {
            _movement = Entity.GetComponent<MovementComponent>();
            if (_movement == null)
            {
                _movement = Entity.AddComponent<MovementComponent>();
            }
            
            _pathfinding = entityManager.Pathfinding;
            _movement.OnDestinationReached += OnDestinationReached;
            ResetIdleTimer();
            
        }

        private void OnDestinationReached()
        {
            // Only reset timer and clear waiting flag when movement is actually complete
            if (_movement.IsMoving)
                return;
        
            ResetIdleTimer();
            _isWaitingForPath = false;
        }

        public void OnTick()
        {
            // Double-check both flags to ensure we don't start new movement too early
            if (_movement.IsMoving || _isWaitingForPath)
            {
                return;
            }

            _idleTimer -= 0.1f;
            
            if (_idleTimer <= 0)
            {
                TryRandomMovement();
            }
        }

        private void TryRandomMovement()
        {
            Vector3 currentPos = transform.position;
            
            // Convert world position to block position correctly
            var blockPos = new int3(
                Mathf.FloorToInt(currentPos.x), // Floor instead of truncate
                Mathf.FloorToInt(currentPos.y),
                Mathf.FloorToInt(currentPos.z)
            );
                        
            var path = _pathfinding.FindRandomPath(
                blockPos,
                _idleMovementRange,
                _entityHeight
            );
            
            if (path.Count > 0)
            {
                Vector3 endPos = path[path.Count - 1];
                float actualDistance = Vector3.Distance(currentPos, endPos);                
                _isWaitingForPath = true;
                _movement.MoveAlongPath(path);
            }
            else
            {
                ResetIdleTimer();
            }
        }

        private void ResetIdleTimer()
        {
            _idleTimer = Random.Range(_minIdleTime, _maxIdleTime);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_movement != null)
            {
                _movement.OnDestinationReached -= OnDestinationReached;
            }
        }
    }
}