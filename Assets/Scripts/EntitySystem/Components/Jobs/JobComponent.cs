using EntitySystem.Core;
using EntitySystem.Core.Jobs;
using EntitySystem.Core.World;
using UnityEngine;
using WorldSystem;

namespace EntitySystem.Components.Jobs
{
    public class JobComponent : GameComponent
    {
        private Job _currentJob;
        private int _ticksSinceLastJob;
        private const int MIN_TICKS_BETWEEN_JOBS = 10;

        public Job CurrentJob => _currentJob;

        protected override void OnTickInternal()
        {
            if (_currentJob != null)
            {
                var status = _currentJob.Execute(Entity);
                if (status != JobStatus.InProgress)
                {
                    _currentJob = null;
                    _ticksSinceLastJob = 0;
                }
                return;
            }

            _ticksSinceLastJob++;
            if (_ticksSinceLastJob < MIN_TICKS_BETWEEN_JOBS)
            {
                return;
            }

            // Get new job (either from job system or create new wander job)
            var jobSystem = Entity.Manager.GetJobSystem();
            if (jobSystem != null)
            {
                _currentJob = jobSystem.GetBestJobFor(Entity);
            }

            if (_currentJob == null)
            {
                var worldSystem = Entity.Manager.GetWorldSystem();
                _currentJob = new WanderJob(worldSystem);
            }
        }
    }
} 