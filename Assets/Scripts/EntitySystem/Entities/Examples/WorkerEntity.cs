using EntitySystem.Core;
using JobSystem.Core;
using JobSystem.Components;
using EntitySystem.Components.Visual;

namespace EntitySystem.Entities.Examples
{
    public class WorkerEntity : Entity
    {
        public WorkerEntity(long id, EntityManager manager) : base(id, manager) { }

        protected override void SetupComponents()
        {
            AddComponent<JobComponent>();
            AddComponent<JobMoverComponent>();
            AddComponent<VisualComponent>();
        }
    }
} 