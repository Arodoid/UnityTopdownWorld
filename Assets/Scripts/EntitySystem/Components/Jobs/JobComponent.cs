using EntitySystem.Core;
using EntitySystem.Core.Jobs;
using UnityEngine;

namespace EntitySystem.Components.Jobs
{
    public class JobComponent : GameComponent
    {
        private Job _currentJob;
        private int _ticksSinceLastJob;
        private const int MIN_TICKS_BETWEEN_JOBS = 10;

        protected override void OnTickInternal()
        {
            Debug.Log($"JobComponent Tick - Entity {Entity.Id} - Current Job: {(_currentJob != null ? "Active" : "None")}");

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
                var worldAccess = Entity.Manager.GetWorldAccess();
                _currentJob = new WanderJob(worldAccess);
            }
        }
    }
} 