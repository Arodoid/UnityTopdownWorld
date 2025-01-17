using UnityEngine;
using EntitySystem.Core.Interfaces;
using EntitySystem.Components.Movement;
using WorldSystem;
using Unity.Mathematics;
using EntitySystem.Core.World;

namespace EntitySystem.Core.Jobs
{
    public class WanderJob : Job
    {
        private readonly IWorldSystem _worldSystem;
        private const float MIN_WANDER_RADIUS = 8f;
        private const float MAX_WANDER_RADIUS = 20f;
        private bool _pathSet = false;

        public WanderJob(IWorldSystem worldSystem) : base(JobPriorities.IDLE, false)
        {
            _worldSystem = worldSystem;
        }

        public override JobStatus Execute(IEntity entity)
        {
            var movement = entity.GetComponent<MovementComponent>();
            if (movement == null) return JobStatus.Failed;

            if (!_pathSet)
            {
                var pathFinder = new PathFinder(_worldSystem);
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