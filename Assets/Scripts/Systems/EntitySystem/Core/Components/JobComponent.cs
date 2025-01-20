using UnityEngine;
using System.Collections.Generic;

namespace EntitySystem.Core.Components
{
    public class JobComponent : EntityComponent, ITickable
    {
        private Queue<IJob> _personalJobs = new();
        private IJob _currentJob;
        private bool _isExecutingJob;
        private IdleMovementComponent _idleMovement;

        protected override void OnInitialize(EntityManager entityManager)
        {
            base.OnInitialize(entityManager);
            _idleMovement = Entity.GetComponent<IdleMovementComponent>();
            
            // Disable idle movement initially since we'll control it
            if (_idleMovement != null)
            {
                _idleMovement.enabled = false;
            }
        }

        public bool HasJob => _currentJob != null;
        public bool IsIdle => !HasJob && _personalJobs.Count == 0;

        public void AddPersonalJob(IJob job)
        {
            _personalJobs.Enqueue(job);
            // Disable idle movement when we get a job
            if (_idleMovement != null)
            {
                _idleMovement.enabled = false;
            }
        }

        public void OnTick()
        {
            if (_isExecutingJob)
            {
                if (_currentJob.Update())
                {
                    // Job is complete
                    _currentJob = null;
                    _isExecutingJob = false;
                }
                return;
            }

            if (_currentJob == null)
            {
                // Try to get a personal job first
                if (_personalJobs.Count > 0)
                {
                    _currentJob = _personalJobs.Dequeue();
                }
                // If no personal jobs, try to get a global job
                else if (Entity.EntityManager.TryGetComponent(out JobSystemComponent jobSystem))
                {
                    _currentJob = jobSystem.TryGetGlobalJob(Entity);
                }
                
                // If we still don't have a job, enable idle movement
                if (_currentJob == null && _idleMovement != null)
                {
                    _idleMovement.enabled = true;
                }
            }

            if (_currentJob != null && !_isExecutingJob)
            {
                // Disable idle movement when starting a job
                if (_idleMovement != null)
                {
                    _idleMovement.enabled = false;
                }
                
                _isExecutingJob = true;
                _currentJob.Start(Entity);
            }
        }

        public void CancelCurrentJob()
        {
            if (_currentJob != null)
            {
                _currentJob.Cancel();
                _currentJob = null;
                _isExecutingJob = false;
                
                // Enable idle movement when job is cancelled
                if (_idleMovement != null)
                {
                    _idleMovement.enabled = true;
                }
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            CancelCurrentJob();
        }
    }
} 