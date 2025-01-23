using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using EntitySystem.Core.Jobs;  // Add this for MineBlockJob

namespace EntitySystem.Core
{
    public class JobSystemComponent : MonoBehaviour
    {
        private Queue<IJob> _globalJobs = new();
        private HashSet<IJob> _inProgressJobs = new();
        private Dictionary<IJob, float> _jobStartTimes = new();
        private Dictionary<System.Type, float> _jobTypePriorities = new();
        private bool _showDebug = true;
        
        private const float JOB_TIMEOUT_SECONDS = 300f; // 5 minutes
        
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
            GUILayout.Label($"In Progress: {_inProgressJobs.Count}");
            
            // Show jobs by type with priorities
            foreach (var jobGroup in _globalJobs.GroupBy(j => j.GetType()))
            {
                float priority = _jobTypePriorities.GetValueOrDefault(jobGroup.Key, 0f);
                GUILayout.Label($"{jobGroup.Key.Name} (P:{priority:F1}): {jobGroup.Count()}");
            }
            
            // Show running times
            if (_inProgressJobs.Count > 0)
            {
                GUILayout.Label("\n<b>Running Jobs</b>");
                foreach (var job in _inProgressJobs)
                {
                    float runTime = Time.time - _jobStartTimes[job];
                    GUILayout.Label($"{job.GetType().Name}: {runTime:F1}s");
                }
            }
            
            GUILayout.EndArea();
        }

        public void AddGlobalJob(IJob job)
        {
            if (job == null) return;
            _globalJobs.Enqueue(job);
        }

        public IJob TryGetJob(Entity worker)
        {
            if (worker == null) return null;

            var jobsByType = _globalJobs
                .Where(job => !_inProgressJobs.Contains(job) && job.CanAssignTo(worker))
                .GroupBy(job => job.GetType())
                .OrderByDescending(group => 
                    _jobTypePriorities.GetValueOrDefault(group.Key, 0f));

            foreach (var jobGroup in jobsByType)
            {
                IJob bestJob = FindBestJobInGroup(jobGroup, worker);
                if (bestJob != null && TryStartJob(bestJob, worker))
                {
                    return bestJob;
                }
            }

            return null;
        }

        private IJob FindBestJobInGroup(IGrouping<System.Type, IJob> jobGroup, Entity worker)
        {
            IJob bestJob = null;
            float bestPriority = float.MinValue;

            foreach (var job in jobGroup)
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

        private bool TryStartJob(IJob job, Entity worker)
        {
            try
            {
                job.Start(worker);
                if (!job.Update()) // Job started successfully
                {
                    _inProgressJobs.Add(job);
                    _jobStartTimes[job] = Time.time;
                    return true;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error starting job {job.GetType().Name}: {e}");
            }
            return false;
        }

        public void OnJobComplete(IJob job)
        {
            if (job == null) return;
            
            _inProgressJobs.Remove(job);
            _jobStartTimes.Remove(job);
            
            // Only remove from queue if the job actually completed its task
            if (job.IsComplete)
            {
                var remainingJobs = new Queue<IJob>();
                while (_globalJobs.Count > 0)
                {
                    var nextJob = _globalJobs.Dequeue();
                    if (nextJob != job)
                    {
                        remainingJobs.Enqueue(nextJob);
                    }
                }
                _globalJobs = remainingJobs;
            }
        }

        public void CancelJob(IJob job)
        {
            if (job == null) return;
            
            try
            {
                job.Cancel();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error cancelling job {job.GetType().Name}: {e}");
            }
            
            _inProgressJobs.Remove(job);
            _jobStartTimes.Remove(job);
        }
    }
} 