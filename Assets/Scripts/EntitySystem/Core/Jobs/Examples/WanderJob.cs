using UnityEngine;
using EntitySystem.Core.Interfaces;
using EntitySystem.Components.Movement;
using EntitySystem.Core.World;
using System.Collections.Generic;

namespace EntitySystem.Core.Jobs
{
    public class WanderJob : Job
    {
        private readonly IWorldAccess _worldAccess;
        private const float WANDER_RADIUS = 5f;
        private bool _pathSet = false;

        public WanderJob(IWorldAccess worldAccess) : base(JobPriorities.WANDER, false)
        {
            _worldAccess = worldAccess;
        }

        public override JobStatus Execute(IEntity entity)
        {
            var movement = entity.GetComponent<MovementComponent>();
            if (movement == null) return JobStatus.Failed;

            // If we haven't set a path yet, try to find one
            if (!_pathSet)
            {
                // Try multiple times to find a valid path
                for (int i = 0; i < 5; i++)
                {
                    Vector2 randomDir = Random.insideUnitCircle.normalized;
                    Vector3 currentPos = entity.GameObject.transform.position;
                    
                    Vector3 targetXZ = new Vector3(
                        currentPos.x + (randomDir.x * WANDER_RADIUS),
                        0,
                        currentPos.z + (randomDir.y * WANDER_RADIUS)
                    );

                    int groundY = _worldAccess.GetHighestSolidBlock(
                        Mathf.RoundToInt(targetXZ.x),
                        Mathf.RoundToInt(targetXZ.z)
                    );

                    if (groundY >= 0)
                    {
                        Vector3 targetPos = new Vector3(targetXZ.x, groundY + 1, targetXZ.z);
                        var pathFinder = new PathFinder(_worldAccess);
                        var path = pathFinder.FindPath(entity.Position, targetPos);
                        
                        if (path != null && path.Count > 0)
                        {
                            movement.SetPath(path);
                            _pathSet = true;
                            break;
                        }
                    }
                }

                // If we couldn't find a path after all attempts
                if (!_pathSet)
                {
                    return JobStatus.Failed;
                }
            }

            // Check if we've reached our destination
            if (!movement.IsMoving())
            {
                return JobStatus.Completed;
            }

            return JobStatus.InProgress;
        }
    }
}