using EntitySystem.Core;
using EntitySystem.Components.Jobs;
using EntitySystem.Components.Movement;
using EntitySystem.Components.Visual;

namespace EntitySystem.Entities.Examples
{
    public class WorkerEntity : Entity
    {
        public WorkerEntity(long id, EntityManager manager) : base(id, manager) { }

        protected override void SetupComponents()
        {
            // Add required components
            AddComponent<MovementComponent>();
            AddComponent<JobComponent>();
            AddComponent<VisualComponent>();
        }
    }
} 