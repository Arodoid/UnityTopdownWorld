using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace EntitySystem.Core
{
    public class JobSystemComponent : MonoBehaviour
    {
        private Queue<IJob> _globalJobs = new();
        private HashSet<IJob> _activeJobs = new();
        private Dictionary<IJob, float> _jobStartTimes = new();
        private Dictionary<System.Type, float> _jobTypePriorities = new();
        private bool _showDebug = true;
        
        private const float JOB_TIMEOUT_SECONDS = 300f; // 5 minutes
        
        private object _jobLock = new object();
        
        private void Update()
        {
            CheckForStuckJobs();
        }

        private void CheckForStuckJobs()
        {
            var currentTime = Time.time;
            var stuckJobs = _jobStartTimes
                .Where(kvp => currentTime - kvp.Value > JOB_TIMEOUT_SECONDS)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var job in stuckJobs)
            {
                Debug.LogWarning($"Job timed out after {JOB_TIMEOUT_SECONDS}s: {job.GetType().Name}");
                CancelJob(job);
            }
        }

        public void SetJobTypePriority(System.Type jobType, float priority)
        {
            _jobTypePriorities[jobType] = priority;
        }

        private void OnGUI()
        {
            if (!_showDebug) return;

            GUI.Box(new Rect(10, Screen.height - 260, 300, 250), "");
            
            GUILayout.BeginArea(new Rect(20, Screen.height - 250, 280, 230));
            
            GUILayout.Label("<b>Job System Status</b>");
            GUILayout.Label($"Global Jobs: {_globalJobs.Count}");
            GUILayout.Label($"Active Jobs: {_activeJobs.Count}");
            
            // Show jobs by type with priorities
            foreach (var jobGroup in _globalJobs.GroupBy(j => j.GetType()))
            {
                float priority = _jobTypePriorities.GetValueOrDefault(jobGroup.Key, 0f);
                GUILayout.Label($"{jobGroup.Key.Name} (P:{priority:F1}): {jobGroup.Count()}");
            }
            
            // Show running times
            if (_activeJobs.Count > 0)
            {
                GUILayout.Label("\n<b>Running Jobs</b>");
                foreach (var job in _activeJobs)
                {
                    float runTime = Time.time - _jobStartTimes[job];
                    GUILayout.Label($"{job.GetType().Name}: {runTime:F1}s");
                }
            }
            
            GUILayout.EndArea();
        }

        public void AddGlobalJob(IJob newJob)
        {
            if (newJob == null) return;
            
            lock (_jobLock)
            {
                // Check both queue and active jobs
                var allJobs = _globalJobs.Concat(_activeJobs);
                
                // If we find any identical job, don't add it
                if (allJobs.Any(existingJob => 
                    existingJob.GetType() == newJob.GetType() && 
                    existingJob.GetHashCode() == newJob.GetHashCode() && 
                    existingJob.Equals(newJob)))
                {
                    return;
                }
                
                _globalJobs.Enqueue(newJob);
                Debug.Log($"Added new job: {newJob.GetType().Name}");
            }
        }

        public IJob TryGetJob(Entity worker)
        {
            lock (_jobLock)
            {
                // Don't assign jobs that are already active
                var availableJobs = _globalJobs.Where(job => !_activeJobs.Contains(job));
                
                var job = FindBestJobFrom(availableJobs, worker);
                if (job != null && job.CanAssignTo(worker))
                {
                    if (_activeJobs.Add(job))
                    {
                        try 
                        {
                            job.Start(worker);
                            _jobStartTimes[job] = Time.time;
                            return job;
                        }
                        catch (System.Exception e)
                        {
                            Debug.LogError($"Failed to start job: {e}");
                            _activeJobs.Remove(job);
                            throw;
                        }
                    }
                }
                return null;
            }
        }

        private IJob FindBestJobFrom(IEnumerable<IJob> jobs, Entity worker)
        {
            IJob bestJob = null;
            float bestPriority = float.MinValue;

            foreach (var job in jobs)
            {
                float priority = job.GetPriority(worker);
                if (priority > bestPriority)
                {
                    bestJob = job;
                    bestPriority = priority;
                }
            }

            return bestJob;
        }

        public void OnJobComplete(IJob job)
        {
            lock (_jobLock)
            {
                if (_activeJobs.Remove(job))
                {
                    _jobStartTimes.Remove(job);
                    // Only remove from queue if actually completed
                    if (job.IsComplete)
                    {
                        _globalJobs = new Queue<IJob>(_globalJobs.Where(j => j != job));
                    }
                }
            }
        }

        public void CancelJob(IJob job)
        {
            lock (_jobLock)
            {
                if (_activeJobs.Remove(job))
                {
                    job.Cancel();
                }
            }
        }

        public bool HasJobFor<T>(System.Func<T, bool> predicate) where T : IJob
        {
            lock (_jobLock)
            {
                var allJobs = _globalJobs.Concat(_activeJobs);
                return allJobs.OfType<T>().Any(predicate);
            }
        }
    }
} 