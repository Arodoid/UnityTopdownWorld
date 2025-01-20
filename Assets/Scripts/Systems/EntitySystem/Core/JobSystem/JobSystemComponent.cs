using UnityEngine;
using System.Collections.Generic;

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

        public IJob TryGetGlobalJob(Entity worker)
        {
            while (_globalJobs.Count > 0)
            {
                var job = _globalJobs.Peek();
                if (job.CanAssignTo(worker))
                {
                    _globalJobs.Dequeue();
                    _activeJobs.Add(job);
                    return job;
                }
                else
                {
                    // If the first job can't be assigned, remove it and try the next
                    _globalJobs.Dequeue();
                }
            }
            return null;
        }

        public void OnJobComplete(IJob job)
        {
            _activeJobs.Remove(job);
        }
    }
} 