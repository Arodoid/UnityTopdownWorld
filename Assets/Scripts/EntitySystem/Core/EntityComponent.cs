namespace EntitySystem.Core
{
    public abstract class EntityComponent : IEntityComponent
    {
        protected Entity Entity { get; private set; }

        public virtual void Initialize(Entity entity)
        {
            Entity = entity;
        }

        public virtual void OnTick() { }
        public virtual void OnDestroy() { }
    }
} 