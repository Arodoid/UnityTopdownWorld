using UnityEngine;
using System.Collections.Generic;
using EntitySystem.Core;
using EntitySystem.Core.Interfaces;
using System.Linq;

namespace EntitySystem.Components.Movement
{
    public class MovementComponent : GameComponent
    {
        private List<Vector3> _currentPath;
        private int _currentPathIndex;
        private bool _isMoving;
        private float _moveSpeed = 20f;
        private float _pathPointThreshold = 0.25f;
        private Vector3? _currentTarget;
        private bool _debugMode = true;
        private const float DEBUG_DURATION = 3f;
        private static readonly Color CURRENT_PATH_COLOR = new Color(0, 1, 0, 1f); // Bright green

        protected override void OnTickInternal()
        {
            if (!_isMoving || !_currentTarget.HasValue) return;

            Vector3 currentPos = Transform.position;
            Vector3 targetPos = _currentTarget.Value;
            
            // Ensure target is block-centered
            targetPos = GetBlockCenteredPosition(targetPos);
            
            Vector3 toTarget = targetPos - currentPos;
            float distanceToTarget = toTarget.magnitude;

            if (distanceToTarget <= _pathPointThreshold)
            {
                // Snap exactly to block center
                Transform.position = targetPos;
                _currentPathIndex++;
                if (_currentPath != null && _currentPathIndex < _currentPath.Count)
                {
                    _currentTarget = GetBlockCenteredPosition(_currentPath[_currentPathIndex]);
                }
                else
                {
                    StopMoving();
                }
                return;
            }

            float moveDistance = _moveSpeed * (1f/20f);
            moveDistance = Mathf.Min(moveDistance, distanceToTarget);
            
            Vector3 movement = toTarget.normalized * moveDistance;
            Transform.position = currentPos + movement;
        }

        private Vector3 GetBlockCenteredPosition(Vector3 position)
        {
            // Round to nearest block
            int blockX = Mathf.RoundToInt(position.x - 0.5f);
            int blockZ = Mathf.RoundToInt(position.z - 0.5f);
            
            // Center of block is at x.5, z.5
            return new Vector3(
                blockX + 0.5f,
                position.y,
                blockZ + 0.5f
            );
        }

        public void SetPath(List<Vector3> path)
        {
            if (path == null || path.Count == 0)
            {
                StopMoving();
                return;
            }

            // Center all waypoints on blocks
            _currentPath = path.Select(p => GetBlockCenteredPosition(p)).ToList();
            _currentPathIndex = 0;
            _isMoving = true;
            _currentTarget = _currentPath[0];
            
            // Immediately center on current block
            Transform.position = GetBlockCenteredPosition(Transform.position);

            // Draw debug path
            if (_debugMode)
            {
                DrawDebugPath();
            }
        }

        private void DrawDebugPath()
        {
            if (_currentPath == null || _currentPath.Count < 2) return;

            // Draw lines between waypoints
            for (int i = 0; i < _currentPath.Count - 1; i++)
            {
                Vector3 start = _currentPath[i];
                Vector3 end = _currentPath[i + 1];
                
                // Draw line slightly above ground to be visible
                start.y += 0.1f;
                end.y += 0.1f;
                
                Debug.DrawLine(start, end, CURRENT_PATH_COLOR, DEBUG_DURATION);
                
                // Draw point markers
                DrawDebugPoint(start);
            }
            
            // Draw final point
            DrawDebugPoint(_currentPath[_currentPath.Count - 1]);
        }

        private void DrawDebugPoint(Vector3 point)
        {
            float size = 0.2f;
            Vector3 up = point + Vector3.up * size;
            Vector3 down = point + Vector3.down * size;
            Vector3 right = point + Vector3.right * size;
            Vector3 left = point + Vector3.left * size;
            Vector3 forward = point + Vector3.forward * size;
            Vector3 back = point + Vector3.back * size;

            Debug.DrawLine(up, down, CURRENT_PATH_COLOR, DEBUG_DURATION);
            Debug.DrawLine(right, left, CURRENT_PATH_COLOR, DEBUG_DURATION);
            Debug.DrawLine(forward, back, CURRENT_PATH_COLOR, DEBUG_DURATION);
        }

        public void EnableDebug(bool enabled)
        {
            _debugMode = enabled;
        }

        public void StopMoving()
        {
            _currentPath = null;
            _currentTarget = null;
            _isMoving = false;
            
            // Ensure we're centered when stopping
            Transform.position = GetBlockCenteredPosition(Transform.position);
        }

        public bool IsMoving() => _isMoving;
    }
}