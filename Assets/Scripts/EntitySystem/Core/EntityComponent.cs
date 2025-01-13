using EntitySystem.Core.Interfaces;

namespace EntitySystem.Core
{
    public abstract class EntityComponent : IEntityComponent
    {
        protected Entity Entity { get; private set; }

        public virtual void Initialize(Entity entity)
        {
            Entity = entity;
            OnInitialize(entity);
        }

        public virtual void OnTick()
        {
            OnTickInternal();
        }

        public virtual void OnDestroy()
        {
            OnDestroyInternal();
        }

        protected virtual void OnInitialize(Entity entity) { }
        protected virtual void OnTickInternal() { }
        protected virtual void OnDestroyInternal() { }
    }
} 