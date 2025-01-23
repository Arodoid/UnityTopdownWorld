using UnityEngine;

namespace EntitySystem.Core.Components
{
    public abstract class EntityComponent : MonoBehaviour, IEntityComponent
    {
        protected Entity Entity { get; private set; }
        
        public void Initialize(Entity entity)
        {
            Entity = entity;
            
            // If this component is tickable, register it with the Entity's TickSystem
            if (this is ITickable tickable)
            {
                entity.EntityManager.TickSystem.Register(tickable);
            }
            
            OnInitialize(entity.EntityManager);
        }

        protected virtual void OnInitialize(EntityManager entityManager) { }

        protected virtual void OnDestroy()
        {
            // Unregister from tick system if needed
            if (this is ITickable tickable && Entity?.EntityManager?.TickSystem != null)
            {
                Entity.EntityManager.TickSystem.Unregister(tickable);
            }
        }
    }
} 