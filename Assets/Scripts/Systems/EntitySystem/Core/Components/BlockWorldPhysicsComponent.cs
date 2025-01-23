using UnityEngine;
using Unity.Mathematics;
using WorldSystem.API;
using System;
using EntitySystem.Core.Utilities;

namespace EntitySystem.Core.Components
{
    public class BlockWorldPhysicsComponent : EntityComponent, ITickable
    {
        [Header("Physics Settings")]
        [SerializeField] private float _fallCheckInterval = 0.1f; // How often to check for falling
        [SerializeField] private int _maxFallDistance = 50; // Safety limit for fall distance
        [SerializeField] private int _moveTicks = 5; // Number of ticks between moves

        private int _currentTick;
        private int _nextMoveTick;
        private float _nextFallCheckTime;
        private bool _isFalling;
        
        // Pathfinding state
        private int3? _targetPosition;
        private Path _currentPath;
        private int _currentPathIndex;
        private Action<MovementResult> _onMovementComplete;
        private int _requiredDistance;

        // Dependencies
        private PathfindingUtility _pathfinding;
        private WorldSystemAPI _worldAPI;
        private TickSystem _tickSystem;
        private EntityManager _entityManager;

        private float _lastPathRequestTime;
        private const float PATH_REQUEST_COOLDOWN = 0.5f;
        private int _pathRetryCount = 0;

        public enum MovementResult
        {
            Reached,        // Actually reached the destination
            Unreachable,    // No valid path found
            Interrupted,    // Movement was stopped manually
            Failed          // Failed due to other reasons (falling, invalid position, etc)
        }

        protected override void OnInitialize(EntityManager entityManager)
        {
            base.OnInitialize(entityManager);
            _pathfinding = entityManager.Pathfinding;
            _worldAPI = entityManager.WorldAPI;
            _tickSystem = entityManager.GetComponent<TickSystem>();
            _entityManager = entityManager;
            SnapToGrid();
        }

        public void OnTick()
        {
            _currentTick++;
            
            // Always check physics first
            CheckPhysics();

            // Only handle movement if we're not falling
            if (!_isFalling && _targetPosition.HasValue && _currentTick >= _nextMoveTick)
            {
                HandleMovement();
            }
        }

        private async void CheckPhysics()
        {
            _nextFallCheckTime = Time.time + _fallCheckInterval;
            
            int3 currentPos = default;
            if (!_entityManager.TryGetEntityPosition(Entity.Id, out currentPos))
            {
                return;
            }
            
            var groundCheckPos = currentPos + new int3(0, -1, 0);
        
            
            bool hasSupport = await _worldAPI.IsBlockSolidAsync(groundCheckPos);
            
            if (!hasSupport)
            {
                if (!_isFalling)
                {
                    StartFalling();
                }
                HandleFalling();
            }
            else if (_isFalling)
            {
                StopFalling();
            }
        }

        private void StartFalling()
        {
            _isFalling = true;
            // Cancel current path - we'll need to recalculate after landing
            _currentPath = null;
        }

        private async void HandleFalling()
        {
            int3 currentPos = default;
            if (!_entityManager.TryGetEntityPosition(Entity.Id, out currentPos))
            {
                return;
            }
            
            for (int i = 1; i <= _maxFallDistance; i++)
            {
                var checkPos = currentPos + new int3(0, -i, 0);
                if (await _worldAPI.IsBlockSolidAsync(checkPos))
                {
                    _entityManager.SetEntityPosition(Entity.Id, checkPos + new int3(0, 1, 0));
                    return;
                }
            }
        }

        private void StopFalling()
        {
            _isFalling = false;
            SnapToGrid();
            
            // If we had a destination, recalculate path from new position
            if (_targetPosition.HasValue)
            {
                RequestNewPath();
            }
        }

        public void MoveTo(int3 destination, int requiredDistance, Action<MovementResult> onComplete)
        {
            _targetPosition = destination;
            _requiredDistance = requiredDistance;
            _onMovementComplete = onComplete;
            RequestNewPath();
        }

        public void Stop()
        {
            CompleteMovement(MovementResult.Interrupted);
        }

        private void HandleMovement()
        {
            if (!_targetPosition.HasValue) return;

            if (IsAtDestination())
            {
                CompleteMovement(MovementResult.Reached);
                return;
            }

            if (_currentPath == null || !_currentPath.IsValid)
            {
                RequestNewPath();
                return;
            }

            MoveAlongPath();
        }

        private void MoveAlongPath()
        {
            if (_currentPath?.Points == null || _currentPathIndex >= _currentPath.Points.Count)
            {
                if (IsAtDestination())
                {
                    CompleteMovement(MovementResult.Reached);
                }
                else
                {
                    RequestNewPath();
                }
                return;
            }

            var nextPosition = _currentPath.Points[_currentPathIndex];
            _entityManager.SetEntityPosition(Entity.Id, nextPosition);
            
            _nextMoveTick = _currentTick + _moveTicks;
            _currentPathIndex++;
        }

        private void RequestNewPath()
        {
            if (!_targetPosition.HasValue) return;
            
            // Prevent path request spam
            if (Time.time - _lastPathRequestTime < PATH_REQUEST_COOLDOWN)
            {
                return;
            }
            
            int3 currentPos;
            if (!_entityManager.TryGetEntityPosition(Entity.Id, out currentPos))
            {
                return;
            }

            _lastPathRequestTime = Time.time;
            _pathfinding.RequestPath(
                Entity,
                currentPos,
                _targetPosition.Value,
                1f, // Entity height
                OnPathReceived
            );
        }

        private void OnPathReceived(Path path)
        {
            if (path == null)
            {
                // Don't immediately fail - give it a few tries
                if (_pathRetryCount++ < 3)
                {
                    return;
                }
                CompleteMovement(MovementResult.Unreachable);
                return;
            }

            _pathRetryCount = 0;
            _currentPath = path;
            _currentPathIndex = 0;
        }

        private bool IsAtDestination()
        {
            if (!_targetPosition.HasValue) return false;
            
            int3 currentPos = default;
            if (!_entityManager.TryGetEntityPosition(Entity.Id, out currentPos))
            {
                return false;
            }
            var dx = math.abs(currentPos.x - _targetPosition.Value.x);
            var dy = math.abs(currentPos.y - _targetPosition.Value.y);
            var dz = math.abs(currentPos.z - _targetPosition.Value.z);
            
            return dx <= _requiredDistance && 
                   dy <= _requiredDistance && 
                   dz <= _requiredDistance;
        }

        private void CompleteMovement(MovementResult result)
        {
            var callback = _onMovementComplete;
            _targetPosition = null;
            _currentPath = null;
            _onMovementComplete = null;
            callback?.Invoke(result);
        }

        private void SnapToGrid()
        {
            int3 currentPos;
            if (_entityManager.TryGetEntityPosition(Entity.Id, out currentPos))
            {
                _entityManager.SetEntityPosition(Entity.Id, currentPos);
            }
        }
    }
}