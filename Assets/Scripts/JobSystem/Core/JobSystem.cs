using UnityEngine;
using System.Collections.Generic;

namespace JobSystem.Core
{
    public class JobSystem : MonoBehaviour
    {
        // Global jobs anyone can take
        private List<Job> _globalJobs = new();
        
        // Personal jobs for each entity
        private Dictionary<long, List<Job>> _personalJobs = new();
        
        // Currently active jobs
        private Dictionary<long, Job> _activeJobs = new();

        public void AddGlobalJob(Job job)
        {
            if (job.IsPersonal)
            {
                Debug.LogError("Attempted to add personal job to global queue");
                return;
            }
            _globalJobs.Add(job);
            _globalJobs.Sort((a, b) => b.Priority.CompareTo(a.Priority)); // Higher priority first
        }

        public void AddPersonalJob(long entityId, Job job)
        {
            if (!job.IsPersonal)
            {
                Debug.LogError("Attempted to add global job to personal queue");
                return;
            }

            if (!_personalJobs.ContainsKey(entityId))
            {
                _personalJobs[entityId] = new List<Job>();
            }
            
            _personalJobs[entityId].Add(job);
            _personalJobs[entityId].Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        public Job GetBestJobFor(long entityId)
        {
            // Check if entity already has an active job
            if (_activeJobs.TryGetValue(entityId, out Job activeJob))
            {
                return activeJob;
            }

            Job bestJob = null;

            // Check personal jobs first
            if (_personalJobs.TryGetValue(entityId, out var personalQueue) && 
                personalQueue.Count > 0)
            {
                bestJob = personalQueue[0];
            }

            // If no personal jobs, check global jobs
            if (bestJob == null && _globalJobs.Count > 0)
            {
                // Get highest priority job the entity can execute
                foreach (var job in _globalJobs)
                {
                    if (job.CanExecute(entityId))
                    {
                        bestJob = job;
                        break;
                    }
                }
            }

            if (bestJob != null)
            {
                AssignJob(entityId, bestJob);
            }

            return bestJob;
        }

        private void AssignJob(long entityId, Job job)
        {
            // Remove from appropriate queue
            if (job.IsPersonal)
            {
                _personalJobs[entityId].Remove(job);
            }
            else
            {
                _globalJobs.Remove(job);
            }

            // Add to active jobs
            job.Assign(entityId);
            _activeJobs[entityId] = job;
        }

        public void CompleteJob(long entityId, Job job)
        {
            if (_activeJobs.TryGetValue(entityId, out var activeJob) && activeJob == job)
            {
                job.Complete();
                _activeJobs.Remove(entityId);
            }
        }

        public void FailJob(long entityId, Job job)
        {
            if (_activeJobs.TryGetValue(entityId, out var activeJob) && activeJob == job)
            {
                job.Fail();
                _activeJobs.Remove(entityId);
            }
        }
    }
} 