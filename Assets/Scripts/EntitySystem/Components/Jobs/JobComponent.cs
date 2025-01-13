using EntitySystem.Core;
using EntitySystem.Core.Jobs;
using UnityEngine;

namespace EntitySystem.Components.Jobs
{
    public class JobComponent : GameComponent
    {
        private Job _currentJob;
        private JobSystem _jobSystem;
        private float _lastJobCheckTime;
        private const float JOB_CHECK_INTERVAL = 1f; // Check for new jobs every second

        protected override void OnInitialize(Entity entity)
        {
            _jobSystem = Entity.Manager.GetJobSystem();
        }

        protected override void OnTickInternal()
        {
            if (_currentJob == null)
            {
                // Only check for new jobs periodically
                if (Time.time >= _lastJobCheckTime + JOB_CHECK_INTERVAL)
                {
                    _lastJobCheckTime = Time.time;
                    
                    // Try to get a job
                    _currentJob = _jobSystem.GetBestJobFor(Entity);
                    
                    // If no other jobs available, create a wander job
                    if (_currentJob == null)
                    {
                        var wanderJob = new WanderJob();
                        _jobSystem.AddPersonalJob(Entity, wanderJob);
                        _currentJob = _jobSystem.GetBestJobFor(Entity);
                    }
                    
                    if (_currentJob != null)
                    {
                    }
                }
                return;
            }

            // Execute current job and log status
            var status = _currentJob.Execute(Entity);
            
            switch (status)
            {
                case JobStatus.Completed:
                    _jobSystem.CompleteJob(Entity, _currentJob);
                    _currentJob = null;
                    break;
                    
                case JobStatus.Failed:
                    _jobSystem.FailJob(Entity, _currentJob);
                    _currentJob = null;
                    break;
                    
                case JobStatus.InProgress:
                    // Log progress periodically
                    if (Time.time >= _lastJobCheckTime + JOB_CHECK_INTERVAL)
                    {
                        _lastJobCheckTime = Time.time;
                    }
                    break;
            }
        }
    }
} 