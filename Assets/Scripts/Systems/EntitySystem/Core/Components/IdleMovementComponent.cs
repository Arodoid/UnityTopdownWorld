using UnityEngine;
using Unity.Mathematics;
using EntitySystem.Core.Utilities;
using Random = UnityEngine.Random;

namespace EntitySystem.Core.Components
{
    public class IdleBlockWorldPhysicsComponent : EntityComponent, ITickable
    {
        [SerializeField] private int _idleMovementRange = 15;
        [SerializeField] private float _minIdleTime = 2f;
        [SerializeField] private float _maxIdleTime = 5f;

        private BlockWorldPhysicsComponent _movement;
        private float _idleTimer;

        protected override void OnInitialize(EntityManager entityManager)
        {
            _movement = Entity.GetComponent<BlockWorldPhysicsComponent>();
            if (_movement == null)
            {
                _movement = Entity.AddComponent<BlockWorldPhysicsComponent>();
            }
            
            ResetIdleTimer();
        }

        public void OnTick()
        {
            // if (_movement.IsMoving)
                return;

            // _idleTimer -= 0.1f;
            
            // if (_idleTimer <= 0)
            // {
                // PickNewDestination();
                // ResetIdleTimer();
            // }
        }

        private void PickNewDestination()
        {
            int3 currentPos;
            if (!Entity.EntityManager.TryGetEntityPosition(Entity.Id, out currentPos))
            {
                Debug.LogError("Failed to get entity position");
                return;
            }
            var randomOffset = new int3(
                Random.Range(-_idleMovementRange, _idleMovementRange),
                0,
                Random.Range(-_idleMovementRange, _idleMovementRange)
            );
            
            _movement.MoveTo(
                currentPos + randomOffset, 
                0,
                null
            );
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
                _movement.Stop();
            }
        }
    }
}