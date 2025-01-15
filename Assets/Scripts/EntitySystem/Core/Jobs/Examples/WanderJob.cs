using UnityEngine;
using EntitySystem.Core.Interfaces;
using EntitySystem.Components.Movement;
using EntitySystem.Core.World;
using System.Collections.Generic;

namespace EntitySystem.Core.Jobs
{
    public class WanderJob : Job
    {
        private readonly DirectWorldAccess _worldAccess;
        private const float MIN_WANDER_RADIUS = 8f;
        private const float MAX_WANDER_RADIUS = 20f;
        private bool _pathSet = false;

        public WanderJob(DirectWorldAccess worldAccess) : base(JobPriorities.IDLE, false)
        {
            _worldAccess = worldAccess;
        }

        public override JobStatus Execute(IEntity entity)
        {
            var movement = entity.GetComponent<MovementComponent>();
            if (movement == null) return JobStatus.Failed;

            if (!_pathSet)
            {
                var pathFinder = new PathFinder(_worldAccess);
                var randomPath = pathFinder.FindRandomAccessiblePosition(
                    entity.Position,
                    MIN_WANDER_RADIUS,
                    MAX_WANDER_RADIUS
                );

                if (randomPath != null)
                {
                    movement.SetPath(randomPath);
                    _pathSet = true;
                }
            }
        
            return movement.IsMoving() ? JobStatus.InProgress : JobStatus.Completed;
        }
    }
}