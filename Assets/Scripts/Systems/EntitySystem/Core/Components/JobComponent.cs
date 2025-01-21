using UnityEngine;
using System.Collections.Generic;

namespace EntitySystem.Core.Components
{
    public class JobComponent : EntityComponent, ITickable
    {
        private Queue<IJob> _personalJobs = new();
        private IJob _currentJob;
        private IdleMovementComponent _idleMovement;
        private JobSystemComponent _jobSystem;
        private TickSystem _tickSystem;

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

        public void Initialize(JobSystemComponent jobSystem, TickSystem tickSystem)
        {
            _jobSystem = jobSystem;
            _tickSystem = tickSystem;
            
            if (_jobSystem == null)
                Debug.LogError($"JobComponent: JobSystemComponent is null for entity {Entity.GetInstanceID()}");
            if (_tickSystem == null)
                Debug.LogError($"JobComponent: TickSystem is null for entity {Entity.GetInstanceID()}");
            else
                _tickSystem.Register(this);
        }

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
            if (_jobSystem == null) return;

            // If we don't have a job, try to get one
            if (_currentJob == null)
            {
                // First check personal jobs
                if (_personalJobs.Count > 0)
                {
                    _currentJob = _personalJobs.Dequeue();
                    _currentJob.Start(Entity);
                }
                // Then check global jobs
                else
                {
                    _currentJob = _jobSystem.TryGetJob(Entity);
                    if (_currentJob != null)
                    {
                        Debug.Log($"Entity {Entity.GetInstanceID()} starting job {_currentJob.GetType().Name}");
                        _currentJob.Start(Entity);
                    }
                    else if (_idleMovement != null)
                    {
                        // Enable idle movement when no job is available
                        _idleMovement.enabled = true;
                    }
                }
            }
            // If we have a job, update it
            else
            {
                bool isComplete = _currentJob.Update();
                if (isComplete)
                {
                    Debug.Log($"Entity {Entity.GetInstanceID()} completed job {_currentJob.GetType().Name}");
                    _jobSystem.OnJobComplete(_currentJob);
                    _currentJob = null;
                    
                    // Re-enable idle movement when job is complete
                    if (_idleMovement != null)
                    {
                        _idleMovement.enabled = true;
                    }
                }
            }
        }

        public void CancelCurrentJob()
        {
            if (_currentJob != null)
            {
                _jobSystem.CancelJob(_currentJob);
                _currentJob = null;
                
                // Re-enable idle movement when job is cancelled
                if (_idleMovement != null)
                {
                    _idleMovement.enabled = true;
                }
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            
            if (_tickSystem != null)
            {
                _tickSystem.Unregister(this);
            }
            
            CancelCurrentJob();
        }
    }
} 