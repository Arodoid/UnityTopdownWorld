using UnityEngine;
using System.Collections.Generic;

namespace EntitySystem.Core.Components
{
    public class JobComponent : EntityComponent, ITickable
    {
        private Queue<IJob> _personalJobs = new();
        private IJob _currentJob;
        // private IdleBlockWorldPhysicsComponent _idleMovement;
        private JobSystemComponent _jobSystem;
        private TickSystem _tickSystem;
        private IdleBlockWorldPhysicsComponent _idleMovement;

        protected override void OnInitialize(EntityManager entityManager)
        {
            base.OnInitialize(entityManager);
            _idleMovement = Entity.GetComponent<IdleBlockWorldPhysicsComponent>();
            
            //Disable idle movement initially since we'll control it
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

            try
            {
                if (_currentJob == null)
                {
                    if (_personalJobs.Count > 0)
                    {
                        _currentJob = _personalJobs.Dequeue();
                        if (!TryStartJob(_currentJob))
                        {
                            _currentJob = null;
                        }
                    }
                    else
                    {
                        _currentJob = _jobSystem.TryGetJob(Entity);
                        if (_currentJob == null && _idleMovement != null)
                        {
                            _idleMovement.enabled = true;
                        }
                    }
                }
                else
                {
                    bool isComplete = _currentJob.Update();
                    if (isComplete)
                    {
                        CompleteCurrentJob();
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error in JobComponent.OnTick: {e}");
                CompleteCurrentJob(); // Clean up on error
            }
        }

        private bool TryStartJob(IJob job)
        {
            try
            {
                job.Start(Entity);
                if (_idleMovement != null)
                {
                    _idleMovement.enabled = false;
                }
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error starting job: {e}");
                return false;
            }
        }

        private void CompleteCurrentJob()
        {
            if (_currentJob != null)
            {
                _jobSystem.OnJobComplete(_currentJob);
                _currentJob = null;
            }
            
            if (_idleMovement != null)
            {
                _idleMovement.enabled = true;
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