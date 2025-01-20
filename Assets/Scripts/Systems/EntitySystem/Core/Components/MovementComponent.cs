using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;
using System;

namespace EntitySystem.Core.Components
{
    public class MovementComponent : EntityComponent, ITickable
    {
        [SerializeField] private float _moveSpeed = 1f; // Blocks per tick
        private List<int3> _currentPath = new();
        private int _currentPathIndex;
        private bool _isMoving;
        private float3 _visualPosition; // Track smooth position

        public event Action OnDestinationReached;
        public bool IsMoving => _isMoving;

        public void MoveTo(List<int3> path)
        {
            _currentPath = path;
            _currentPathIndex = 0;
            _isMoving = true;
            _visualPosition = Entity.Position; // Initialize visual position
        }

        public void Stop()
        {
            _isMoving = false;
            _currentPath.Clear();
        }

        public void OnTick()
        {
            if (!_isMoving || _currentPath.Count == 0)
            {
                return;
            }
            
            var targetPos = _currentPath[_currentPathIndex];
            var currentPos = Entity.Position;
            
            
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
                var diff = targetPos - currentPos;
                float3 movement = new float3(0, 0, 0);
                
                if (math.abs(diff.x) >= math.abs(diff.y) && math.abs(diff.x) >= math.abs(diff.z))
                {
                    movement.x = math.sign(diff.x) * _moveSpeed;
                }
                else if (math.abs(diff.y) >= math.abs(diff.z))
                {
                    movement.y = math.sign(diff.y) * _moveSpeed;
                }
                else
                {
                    movement.z = math.sign(diff.z) * _moveSpeed;
                }

                _visualPosition += movement;
                
                Entity.Position = new int3(
                    (int)_visualPosition.x,
                    (int)_visualPosition.y,
                    (int)_visualPosition.z
                );

                transform.position = new Vector3(
                    _visualPosition.x + 0.5f,
                    _visualPosition.y,
                    _visualPosition.z + 0.5f
                );
            }
        }
    }
} 