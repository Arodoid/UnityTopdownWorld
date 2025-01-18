using UnityEngine;
using EntitySystem.Core.Interfaces;
using EntitySystem.Core.Types;

namespace EntitySystem.Core
{
    public abstract class EntityComponent : IEntityComponent
    {
        protected Entity Entity { get; private set; }
        
        // Commonly used properties from GameComponent
        protected Transform Transform => Entity.GameObject.transform;
        protected Vector3 Position => Entity.Position;
        
        // Helper method for getting other components
        protected T Get<T>() where T : class, IEntityComponent 
            => Entity.GetComponent<T>();
            
        // Helper method for casting self to derived type
        protected T Self<T>() where T : EntityComponent => (T)this;

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

        public virtual void OnStateChanged(EntityState oldState, EntityState newState) { }
        
        public virtual void OnPositionChanged(Vector3 oldPosition, Vector3 newPosition) { }

        // Protected virtual methods for derived classes
        protected virtual void OnInitialize(Entity entity) { }
        protected virtual void OnTickInternal() { }
        protected virtual void OnDestroyInternal() { }
    }
} 