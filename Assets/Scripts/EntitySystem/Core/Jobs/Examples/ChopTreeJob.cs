using UnityEngine;
using EntitySystem.Core;
using EntitySystem.Core.Interfaces;
using EntitySystem.Components.Movement;

namespace EntitySystem.Core.Jobs
{
    public class ChopTreeJob : Job
    {
        private float _chopProgress;
        private const float CHOP_TIME = 3f;

        public ChopTreeJob(Vector3 treePosition) : base(JobPriorities.CHOP_WOOD)
        {
            _targetPosition = treePosition;
        }

        public override bool CanExecute(IEntity entity)
        {
            // Check if entity has required components
            return entity.HasComponent<MovementComponent>();
        }

        public override JobStatus Execute(IEntity entity)
        {
            var movement = entity.GetComponent<MovementComponent>();
            
            // First, move to tree
            if (Vector3.Distance(entity.Position, _targetPosition) > 1f)
            {
                movement.SetDestination(_targetPosition);
                return JobStatus.InProgress;
            }

            // Then chop tree
            _chopProgress += Time.deltaTime;
            if (_chopProgress >= CHOP_TIME)
            {
                // Tree chopped!
                return JobStatus.Completed;
            }

            return JobStatus.InProgress;
        }
    }
} 