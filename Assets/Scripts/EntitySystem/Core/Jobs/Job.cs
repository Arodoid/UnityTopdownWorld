using UnityEngine;
using EntitySystem.Core.Interfaces;

namespace EntitySystem.Core.Jobs
{
    public class Job
    {
        public float Priority { get; }
        public bool IsPersonal { get; }
        public long? AssignedEntityId { get; private set; }
        public JobStatus Status { get; private set; } = JobStatus.Pending;

        protected Vector3 _targetPosition;

        public Job(float priority, bool isPersonal = false)
        {
            Priority = priority;
            IsPersonal = isPersonal;
        }

        public virtual bool CanExecute(IEntity entity) => true;
        
        public virtual JobStatus Execute(IEntity entity)
        {
            // Base implementation - override in specific jobs
            return JobStatus.Completed;
        }

        public Vector3 GetTargetPosition() => _targetPosition;

        public void Assign(IEntity entity)
        {
            AssignedEntityId = entity.Id;
            Status = JobStatus.InProgress;
        }

        public void Complete()
        {
            Status = JobStatus.Completed;
            AssignedEntityId = null;
        }

        public void Fail()
        {
            Status = JobStatus.Failed;
            AssignedEntityId = null;
        }
    }

    public enum JobStatus
    {
        Pending,
        InProgress,
        Completed,
        Failed,
        Interrupted
    }
} 