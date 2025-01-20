using UnityEngine;

namespace EntitySystem.Core.Components
{
    public abstract class EntityComponent : MonoBehaviour, IEntityComponent
    {
        protected Entity Entity { get; private set; }
        private TickSystem _tickSystem;

        public void Initialize(Entity entity)
        {
            Entity = entity;
            
            // Get reference to tick system and entity manager
            var entityManager = Entity.GetComponentInParent<EntityManager>();
            _tickSystem = entityManager.GetComponent<TickSystem>();
            
            // If this component is tickable, register it
            if (this is ITickable tickable && _tickSystem != null)
            {
                _tickSystem.Register(tickable);
            }
            
            OnInitialize(entityManager);
        }

        protected virtual void OnInitialize(EntityManager entityManager) { }

        protected virtual void OnDestroy()
        {
            // Unregister from tick system if needed
            if (this is ITickable tickable && _tickSystem != null)
            {
                _tickSystem.Unregister(tickable);
            }
        }
    }
} 