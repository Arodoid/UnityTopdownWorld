using UnityEngine;
using EntitySystem.API;
using WorldSystem;
using Utilities.Navigation;

namespace JobSystem.Core
{
    public class WanderJob : Job
    {
        private readonly IWorldSystem _worldSystem;
        private readonly EntitySystemAPI _entitySystem;
        private const float MIN_WANDER_RADIUS = 8f;
        private const float MAX_WANDER_RADIUS = 20f;
        private bool _pathSet = false;

        public WanderJob(IWorldSystem worldSystem, EntitySystemAPI entitySystem) 
            : base(JobPriorities.IDLE, false)
        {
            _worldSystem = worldSystem;
            _entitySystem = entitySystem;
        }

        public override JobStatus Execute(long entityId)
        {
            var entity = _entitySystem.GetEntity(entityId);
            if (!entity.HasComponent("JobMoverComponent")) 
                return JobStatus.Failed;

            if (!_pathSet)
            {
                var position = entity.Position;
                var pathFinder = new PathFinder(_worldSystem);
                var randomPath = pathFinder.FindRandomAccessiblePosition(
                    position,
                    MIN_WANDER_RADIUS,
                    MAX_WANDER_RADIUS
                );

                if (randomPath != null)
                {
                    entity.SetComponentValue("JobMoverComponent", randomPath);
                    _pathSet = true;
                }
            }
        
            var isMoving = entity.GetComponentValue<bool>("JobMoverComponent");
            return isMoving ? JobStatus.InProgress : JobStatus.Completed;
        }
    }
}