using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using EntitySystem.Core.Interfaces;

namespace EntitySystem.Core.Jobs
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

        public void AddPersonalJob(IEntity entity, Job job)
        {
            if (!job.IsPersonal)
            {
                Debug.LogError("Attempted to add global job to personal queue");
                return;
            }

            if (!_personalJobs.ContainsKey(entity.Id))
            {
                _personalJobs[entity.Id] = new List<Job>();
            }
            
            _personalJobs[entity.Id].Add(job);
            _personalJobs[entity.Id].Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        public Job GetBestJobFor(IEntity entity)
        {
            // Check if entity already has an active job
            if (_activeJobs.TryGetValue(entity.Id, out Job activeJob))
            {
                return activeJob;
            }


            Job bestJob = null;

            // Check personal jobs first
            if (_personalJobs.TryGetValue(entity.Id, out var personalQueue) && 
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
                    if (job.CanExecute(entity))
                    {
                        bestJob = job;
                        break;
                    }
                    else
                    {
                    }
                }
            }

            if (bestJob != null)
            {
                AssignJob(entity, bestJob);
            }
            else
            {
            }

            return bestJob;
        }

        private void AssignJob(IEntity entity, Job job)
        {
            // Remove from appropriate queue
            if (job.IsPersonal)
            {
                _personalJobs[entity.Id].Remove(job);
            }
            else
            {
                _globalJobs.Remove(job);
            }

            // Add to active jobs
            job.Assign(entity);
            _activeJobs[entity.Id] = job;
        }

        public void CompleteJob(IEntity entity, Job job)
        {
            if (_activeJobs.TryGetValue(entity.Id, out var activeJob) && activeJob == job)
            {
                job.Complete();
                _activeJobs.Remove(entity.Id);
            }
        }

        public void FailJob(IEntity entity, Job job)
        {
            if (_activeJobs.TryGetValue(entity.Id, out var activeJob) && activeJob == job)
            {
                job.Fail();
                _activeJobs.Remove(entity.Id);
            }
        }
    }
} 