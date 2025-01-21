using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace EntitySystem.Core
{
    public class JobSystemComponent : MonoBehaviour
    {
        private Queue<IJob> _globalJobs = new();
        private HashSet<IJob> _activeJobs = new();

        public void AddGlobalJob(IJob job)
        {
            _globalJobs.Enqueue(job);
        }

        public IJob TryGetJob(Entity worker)
        {
            // Create a temporary list to hold jobs we need to skip
            var skippedJobs = new List<IJob>();

            while (_globalJobs.Count > 0)
            {
                var job = _globalJobs.Dequeue();

                // Check if job can be assigned to this worker
                if (job.CanAssignTo(worker))
                {
                    // Add to active jobs
                    _activeJobs.Add(job);
                    
                    // Put skipped jobs back in queue
                    foreach (var skippedJob in skippedJobs)
                    {
                        _globalJobs.Enqueue(skippedJob);
                    }
                    
                    return job;
                }
                else
                {
                    // Save this job for later
                    skippedJobs.Add(job);
                }
            }

            // No suitable job found, put skipped jobs back
            foreach (var skippedJob in skippedJobs)
            {
                _globalJobs.Enqueue(skippedJob);
            }

            return null;
        }

        public void OnJobComplete(IJob job)
        {
            _activeJobs.Remove(job);
        }

        public void CancelJob(IJob job)
        {
            if (_activeJobs.Contains(job))
            {
                job.Cancel();
                _activeJobs.Remove(job);
            }
        }
    }
} 