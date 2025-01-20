using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;
using System;

namespace EntitySystem.Core.Components
{
    public class MovementComponent : EntityComponent, ITickable
    {
        private List<int3> _currentPath = new();
        private int _currentPathIndex;
        private bool _isMoving;

        public event Action OnDestinationReached;
        public bool IsMoving => _isMoving;

        public void MoveTo(List<int3> path)
        {
            _currentPath = path;
            _currentPathIndex = 0;
            _isMoving = true;
        }

        public void Stop()
        {
            _isMoving = false;
            _currentPath.Clear();
        }

        public void OnTick()
        {
            if (!_isMoving || _currentPath.Count == 0) return;

            var targetPos = _currentPath[_currentPathIndex];
            var currentPos = Entity.Position;
            
            // If we're at the target position OR very close, move to next point
            if (currentPos.Equals(targetPos))
            {
                _currentPathIndex++;
                
                if (_currentPathIndex >= _currentPath.Count)
                {
                    _isMoving = false;
                    OnDestinationReached?.Invoke();
                    return;
                }
            }
            else
            {
                // For integer grid movement, just move one unit in the primary direction
                var diff = targetPos - currentPos;
                var newPos = currentPos;
                
                // Move one step in the direction with the largest difference
                if (math.abs(diff.x) >= math.abs(diff.y) && math.abs(diff.x) >= math.abs(diff.z))
                {
                    newPos.x += math.sign(diff.x);
                }
                else if (math.abs(diff.y) >= math.abs(diff.z))
                {
                    newPos.y += math.sign(diff.y);
                }
                else
                {
                    newPos.z += math.sign(diff.z);
                }

                Entity.Position = newPos;
                // Adjust the visual position to be centered on the block
                transform.position = new Vector3(
                    newPos.x + 0.5f,
                    newPos.y,
                    newPos.z + 0.5f
                );
            }
        }
    }
} 