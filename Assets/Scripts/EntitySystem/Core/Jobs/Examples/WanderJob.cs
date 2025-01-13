using UnityEngine;
using EntitySystem.Core.Interfaces;
using EntitySystem.Components.Movement;

namespace EntitySystem.Core.Jobs
{
    public class WanderJob : Job
    {
        private float _wanderRadius = 10f;
        private bool _hasDestination = false;

        public WanderJob() : base(JobPriorities.WANDER, isPersonal: true)
        {
        }

        public override bool CanExecute(IEntity entity)
        {
            return entity.HasComponent<MovementComponent>();
        }

        public override JobStatus Execute(IEntity entity)
        {
            var movement = entity.GetComponent<MovementComponent>();

            // If we haven't picked a destination yet
            if (!_hasDestination)
            {
                // Pick random point around current position
                var randomAngle = Random.Range(0f, 360f);
                var randomDistance = Random.Range(0f, _wanderRadius);
                var offset = Quaternion.Euler(0, randomAngle, 0) * Vector3.forward * randomDistance;
                _targetPosition = entity.Position + offset;
                
                // Let MovementComponent handle all the validation and pathfinding
                if (movement.SetDestination(_targetPosition))
                {
                    _hasDestination = true;
                    return JobStatus.InProgress;
                }
                
                return JobStatus.Failed;
            }

            // Let MovementComponent handle the actual movement
            if (!movement.IsMoving())
            {
                return JobStatus.Completed;
            }

            return JobStatus.InProgress;
        }
    }
}