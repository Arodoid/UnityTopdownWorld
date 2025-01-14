using UnityEngine;
using System.Collections.Generic;
using EntitySystem.Core;

namespace EntitySystem.Components.Movement
{
    public class MovementComponent : GameComponent
    {
        private List<Vector3> _currentPath;
        private int _currentPathIndex;
        private bool _isMoving;
        private float _moveSpeed = 25f;
        private float _pathPointThreshold = 0.1f;
        private Vector3? _currentTarget;

        public bool IsMoving() => _isMoving;

        public void SetPath(List<Vector3> path)
        {
            if (path == null || path.Count == 0)
            {
                StopMoving();
                return;
            }

            _currentPath = path;
            _currentPathIndex = 0;
            _isMoving = true;
            _currentTarget = _currentPath[0];
            Debug.Log($"Entity {Entity.Id} starting new path with {path.Count} waypoints");
        }

        public void StopMoving()
        {
            _currentPath = null;
            _currentTarget = null;
            _isMoving = false;
        }

        protected override void OnTickInternal()
        {
            if (!_isMoving || !_currentTarget.HasValue) return;

            Vector3 currentPos = Transform.position;
            Vector3 targetPos = _currentTarget.Value;
            
            float distanceToTarget = Vector3.Distance(
                new Vector3(currentPos.x, targetPos.y, currentPos.z),
                targetPos);

            if (distanceToTarget <= _pathPointThreshold)
            {
                _currentPathIndex++;
                
                if (_currentPath != null && _currentPathIndex < _currentPath.Count)
                {
                    _currentTarget = _currentPath[_currentPathIndex];
                }
                else
                {
                    StopMoving();
                    return;
                }
            }

            // Calculate movement
            Vector3 moveDirection = (targetPos - currentPos).normalized;
            Vector3 newPosition = currentPos + (moveDirection * _moveSpeed * Time.deltaTime);
            
            // Only update Transform
            Transform.position = newPosition;
        }
    }
}