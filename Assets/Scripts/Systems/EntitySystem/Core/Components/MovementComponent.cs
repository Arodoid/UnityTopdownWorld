using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;
using System;
using EntitySystem.Core.Utilities;

namespace EntitySystem.Core.Components
{
    public class MovementComponent : EntityComponent, ITickable
    {
        [SerializeField] private float _moveSpeed = 32f;
        [SerializeField] private float _entityHeight = 1f;
        
        private List<Vector3> _currentPath = new();
        private int _currentPathIndex = 1;
        private bool _isMoving;
        private Vector3 _currentVelocity;
        private const float BASE_SMOOTHING = 0.01f;
        private const float WAYPOINT_THRESHOLD = 0.15f;

        private PathfindingUtility _pathfinding;

        public event Action OnDestinationReached;
        public bool IsMoving => _isMoving;

        protected override void OnInitialize(EntityManager entityManager)
        {
            base.OnInitialize(entityManager);
            _pathfinding = entityManager.Pathfinding;
        }

        public bool MoveTo(int3 destination)
        {
            var currentPos = new int3(
                Mathf.FloorToInt(transform.position.x),
                Mathf.FloorToInt(transform.position.y),
                Mathf.FloorToInt(transform.position.z)
            );

            var path = _pathfinding.FindPath(
                currentPos,
                destination,
                _entityHeight
            );

            if (path.Count > 0)
            {
                MoveAlongPath(path);
                return true;
            }
            
            Debug.LogWarning($"Could not find path to destination: {destination}");
            return false;
        }

        public void MoveAlongPath(List<Vector3> path)
        {
            if (path.Count > 0)
            {
                _currentPath = path;
                _currentPathIndex = 1;
                _isMoving = true;
                _currentVelocity = Vector3.zero;
            }
        }

        public void Stop()
        {
            _isMoving = false;
            _currentPath.Clear();
            _currentVelocity = Vector3.zero;
        }

        public void OnTick()
        {
            if (!_isMoving || _currentPathIndex >= _currentPath.Count) 
                return;

            Vector3 currentTarget = _currentPath[_currentPathIndex];
            float distanceToTarget = Vector3.Distance(transform.position, currentTarget);

            // Look ahead to next waypoint if we're close enough
            Vector3 lookAheadTarget = currentTarget;
            if (_currentPathIndex < _currentPath.Count - 1 && distanceToTarget < WAYPOINT_THRESHOLD)
            {
                lookAheadTarget = Vector3.Lerp(
                    currentTarget, 
                    _currentPath[_currentPathIndex + 1], 
                    1 - (distanceToTarget / WAYPOINT_THRESHOLD)
                );
            }

            // Smooth movement
            float adjustedSmoothing = BASE_SMOOTHING * (4f / _moveSpeed);
            adjustedSmoothing = Mathf.Clamp(adjustedSmoothing, 0.01f, 0.08f);

            transform.position = Vector3.SmoothDamp(
                transform.position,
                lookAheadTarget,
                ref _currentVelocity,
                adjustedSmoothing,
                _moveSpeed * 1.5f
            );

            // Move to next waypoint if we're close enough
            if (distanceToTarget < 0.1f)
            {
                _currentPathIndex++;
                
                // Check if we've reached the end
                if (_currentPathIndex >= _currentPath.Count)
                {
                    _isMoving = false;
                    OnDestinationReached?.Invoke();
                }
            }
        }
    }
}