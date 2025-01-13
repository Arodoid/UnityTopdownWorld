using UnityEngine;
using System.Collections.Generic;
using EntitySystem.Core;
using EntitySystem.Core.World;
using Unity.Mathematics;

namespace EntitySystem.Components.Movement
{
    public class MovementComponent : EntityComponent
    {
        private float _speed = 5f;
        private Vector3? _destination;
        private bool _isMoving;
        private PathFinder _pathFinder;
        private bool _debugMode = true;
        private List<Vector3> _currentPath;
        private int _currentWaypoint;

        public override void Initialize(Entity entity)
        {
            base.Initialize(entity);
            _pathFinder = new PathFinder(Entity.Manager.GetWorldAccess());
        }

        public bool SetDestination(Vector3 destination)
        {
            if (!_pathFinder.IsPathPossible(Entity.Position, destination))
            {
                if (_debugMode) Debug.LogWarning($"[Entity {Entity.Id}] No valid path to {destination}");
                return false;
            }

            _currentPath = _pathFinder.FindPath(Entity.Position, destination);
            _currentWaypoint = 0;
            _isMoving = true;
            return true;
        }

        protected override void OnTickInternal()
        {
            if (!_isMoving || _currentPath == null) return;

            // Move towards current waypoint
            Vector3 target = _currentPath[_currentWaypoint];
            Vector3 direction = (target - Entity.Position).normalized;
            Entity.Position += direction * Time.deltaTime * _speed;

            // Check if reached waypoint
            if (Vector3.Distance(Entity.Position, target) < 0.1f)
            {
                _currentWaypoint++;
                if (_currentWaypoint >= _currentPath.Count)
                {
                    _isMoving = false;
                    _currentPath = null;
                }
            }
        }

        public bool IsMoving() => _isMoving;
    }
}