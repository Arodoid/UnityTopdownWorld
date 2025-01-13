using UnityEngine;
using EntitySystem.Core;
using EntitySystem.Components.Movement;
using EntitySystem.Core.World;

namespace EntitySystem.Components.AI
{
    public class WanderBehaviorComponent : GameComponent
    {
        private float _wanderRadius = 10f;
        private float _nextWanderTime;
        private float _wanderInterval = 5f;
        
        private MovementComponent _movement;
        private PathFinder _pathFinder;
        private bool _debugMode = true;

        protected override void OnInitialize(Entity entity)
        {
            _movement = Entity.GetComponent<MovementComponent>();
            _pathFinder = new PathFinder(Entity.Manager.GetWorldAccess());
            _nextWanderTime = Time.time + Random.Range(0f, _wanderInterval);
            
            if (_debugMode) Debug.Log($"[Entity {Entity.Id}] WanderBehavior initialized");
        }

        protected override void OnTickInternal()
        {
            if (_movement.IsMoving())
            {
                if (_debugMode) Debug.Log($"[Entity {Entity.Id}] Still moving, skipping wander check");
                return;
            }
            
            if (Time.time >= _nextWanderTime)
            {
                if (_debugMode) Debug.Log($"[Entity {Entity.Id}] Time to wander! Current: {Time.time}, Next: {_nextWanderTime}");
                TryWander();
                _nextWanderTime = Time.time + _wanderInterval;
            }
        }

        private void TryWander()
        {
            if (_debugMode) Debug.Log($"[Entity {Entity.Id}] Attempting to find wander destination from {Position}");
            
            var randomDestination = _pathFinder.FindRandomAccessiblePosition(
                Position,
                2f,
                _wanderRadius
            );

            if (randomDestination.HasValue)
            {
                if (_debugMode) Debug.Log($"[Entity {Entity.Id}] Found potential destination: {randomDestination.Value}");
                
                if (_movement.SetDestination(randomDestination.Value))
                {
                    if (_debugMode) Debug.Log($"[Entity {Entity.Id}] Successfully set destination to {randomDestination.Value}");
                    return;
                }
                else
                {
                    if (_debugMode) Debug.Log($"[Entity {Entity.Id}] Failed to set destination despite finding valid position");
                }
            }

            if (_debugMode) Debug.LogWarning($"[Entity {Entity.Id}] Failed to find valid wander destination");
        }
    }
} 