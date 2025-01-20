using UnityEngine;
using System.Collections.Generic;
using EntitySystem.Core.Components;

namespace EntitySystem.Core
{
    public class EntityRegistry : MonoBehaviour
    {
        private Dictionary<string, System.Action<Entity>> _entityTemplates = new();

        private void Awake()
        {
            RegisterDefaultEntities();
        }

        private void RegisterDefaultEntities()
        {
            
            // Living entities
            RegisterEntity("Dog", (entity) => {
                var health = entity.AddComponent<HealthComponent>();
                health._maxHealth = 100f;
                
                var visual = entity.GetComponent<EntityVisualComponent>();
                visual.SetColor(new Color(0.6f, 0.4f, 0.2f));  // Brown color
                
                // Add movement components
                var movement = entity.AddComponent<MovementComponent>();
                var idle = entity.AddComponent<IdleMovementComponent>();
                idle._idleMovementRange = 5f;  // Dogs wander in small area
                idle._entityHeight = 1f;  // Dog height
            });

            RegisterEntity("Colonist", (entity) => {
                var health = entity.AddComponent<HealthComponent>();
                health._maxHealth = 150f;  // Colonists are tougher than dogs
                
                var visual = entity.GetComponent<EntityVisualComponent>();
                visual.SetColor(new Color(0.2f, 0.6f, 1f));  // Blue for colonists
            });

            RegisterEntity("Deer", (entity) => {
                var health = entity.AddComponent<HealthComponent>();
                health._maxHealth = 80f;
                
                var visual = entity.GetComponent<EntityVisualComponent>();
                visual.SetColor(new Color(0.8f, 0.7f, 0.6f));
                
                // Add movement components with different settings
                var movement = entity.AddComponent<MovementComponent>();
                var idle = entity.AddComponent<IdleMovementComponent>();
                idle._idleMovementRange = 10f;  // Deer wander in larger area
                idle._entityHeight = 1.8f;  // Deer height
            });

            // Furniture entities
            RegisterEntity("WoodenChair", (entity) => {
                var health = entity.AddComponent<HealthComponent>();
                health._maxHealth = 50f;
                
                var visual = entity.GetComponent<EntityVisualComponent>();
                visual.SetColor(new Color(0.4f, 0.3f, 0.2f));  // Darker brown for wood
            });
        }

        public void RegisterEntity(string id, System.Action<Entity> setup)
        {
            _entityTemplates[id] = setup;
        }

        public bool TryGetTemplate(string id, out System.Action<Entity> setup)
        {
            return _entityTemplates.TryGetValue(id, out setup);
        }

        public IEnumerable<string> GetAvailableTemplates()
        {
            return _entityTemplates.Keys;
        }
    }
} 