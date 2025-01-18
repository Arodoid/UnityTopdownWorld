using UnityEngine;
using EntitySystem.API;
using EntitySystem.Core;
using EntitySystem.Core.Types;
using EntitySystem.Core.Interfaces;
using WorldSystem;

namespace JobSystem.Core
{
    public class JobComponent : IEntityComponent
    {
        private Entity _entity;
        private EntitySystemAPI _entityAPI;
        private JobSystem _jobSystem;
        private IWorldSystem _worldSystem;
        private Job _currentJob;
        private int _ticksSinceLastJob;
        private const int MIN_TICKS_BETWEEN_JOBS = 10;

        public Job CurrentJob => _currentJob;

        public JobComponent()
        {
        }

        public void Initialize(Entity entity)
        {
            _entity = entity;
            _entityAPI = entity.Manager.GetComponent<EntitySystemAPI>();
            _jobSystem = entity.Manager.GetJobSystem();
            _worldSystem = entity.Manager.GetWorldSystem();
        }

        public void OnTick()
        {
            if (_currentJob != null)
            {
                var status = _currentJob.Execute(_entity.Id);
                if (status != JobStatus.InProgress)
                {
                    _currentJob = null;
                    _ticksSinceLastJob = 0;
                }
                return;
            }

            _ticksSinceLastJob++;
            if (_ticksSinceLastJob < MIN_TICKS_BETWEEN_JOBS)
            {
                return;
            }

            if (_jobSystem != null)
            {
                _currentJob = _jobSystem.GetBestJobFor(_entity.Id);
            }

            if (_currentJob == null)
            {
                _currentJob = new WanderJob(_worldSystem, _entityAPI);
            }
        }

        public void OnDestroy()
        {
            if (_currentJob != null)
            {
                _jobSystem?.FailJob(_entity.Id, _currentJob);
                _currentJob = null;
            }
        }

        public void OnStateChanged(EntityState oldState, EntityState newState)
        {
            if (newState == EntityState.Pooled && _currentJob != null)
            {
                _jobSystem?.FailJob(_entity.Id, _currentJob);
                _currentJob = null;
            }
        }

        public void OnPositionChanged(Vector3 oldPosition, Vector3 newPosition)
        {
            // Handle position changes if needed
        }
    }
} 