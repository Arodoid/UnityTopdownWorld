using UnityEngine;
using System;
using System.Collections.Generic;
using EntitySystem.Data;
using EntitySystem.Core.Components;

namespace EntitySystem.Core
{
    [System.Serializable]
    public class EntityRegistry
    {
        private class EntityTemplate
        {
            public string Name;
            public EntityType Type;
            public Action<Entity> Setup;
        }

        private readonly Dictionary<string, EntityTemplate> _templates = new();
        private JobSystemComponent _jobSystem;
        private TickSystem _tickSystem;

        public EntityRegistry()
        {
            RegisterDefaultTemplates();
        }

        public void SetSystems(JobSystemComponent jobSystem, TickSystem tickSystem)
        {
            _jobSystem = jobSystem;
            _tickSystem = tickSystem;
            RegisterDefaultTemplates(); // Re-register with systems
        }

        private void RegisterDefaultTemplates()
        {
            // Living entities
            RegisterTemplate("Colonist", EntityType.Living, entity => {
                // Add all components first
                entity.AddComponent<HealthComponent>();
                entity.AddComponent<MovementComponent>();
                entity.AddComponent<InventoryComponent>();
                entity.AddComponent<ItemPickupComponent>();
                entity.AddComponent<EntityVisualComponent>();
                var job = entity.AddComponent<JobComponent>();
                
                // Then configure them
                entity.GetComponent<HealthComponent>()._maxHealth = 150f;
                entity.GetComponent<EntityVisualComponent>().SetColor(new Color(0.2f, 0.6f, 1f));
                job.Initialize(_jobSystem, _tickSystem);
            });

            RegisterTemplate("Dog", EntityType.Living, entity => {
                // Add components first
                entity.AddComponent<HealthComponent>();
                entity.AddComponent<MovementComponent>();
                entity.AddComponent<IdleMovementComponent>();
                entity.AddComponent<EntityVisualComponent>();
                
                // Then configure them
                entity.GetComponent<HealthComponent>()._maxHealth = 100f;
                entity.GetComponent<IdleMovementComponent>()._idleMovementRange = 10f;
                entity.GetComponent<IdleMovementComponent>()._entityHeight = 1f;
                entity.GetComponent<EntityVisualComponent>().SetColor(new Color(0.6f, 0.4f, 0.2f));
            });

            // Items
            RegisterTemplate("WoodItem", EntityType.Item, entity => {
                entity.AddComponent<ItemComponent>();
                entity.AddComponent<EntityVisualComponent>();
                
                var item = entity.GetComponent<ItemComponent>();
                item.ItemId = "Wood";
                item.SpaceRequired = 2;
                
                entity.GetComponent<EntityVisualComponent>().SetColor(new Color(0.6f, 0.4f, 0.2f));
            });

            RegisterTemplate("StoneItem", EntityType.Item, entity => {
                entity.AddComponent<ItemComponent>();
                entity.AddComponent<EntityVisualComponent>();
                
                var item = entity.GetComponent<ItemComponent>();
                item.ItemId = "Stone";
                item.SpaceRequired = 3;
                
                entity.GetComponent<EntityVisualComponent>().SetColor(new Color(0.7f, 0.7f, 0.7f));
            });

            // Furniture
            RegisterTemplate("WoodenChair", EntityType.Furniture, entity => {
                entity.AddComponent<HealthComponent>();
                entity.AddComponent<EntityVisualComponent>();
                
                entity.GetComponent<HealthComponent>()._maxHealth = 50f;
                entity.GetComponent<EntityVisualComponent>().SetColor(new Color(0.4f, 0.3f, 0.2f));
            });
        }

        public void RegisterTemplate(string name, EntityType type, Action<Entity> setup)
        {
            if (_templates.ContainsKey(name))
            {
                Debug.LogError($"Template '{name}' is already registered!");
                return;
            }

            _templates[name] = new EntityTemplate 
            { 
                Name = name, 
                Type = type, 
                Setup = setup 
            };
        }

        public bool TryGetTemplate(string name, out (EntityType Type, Action<Entity> Setup) template)
        {
            if (_templates.TryGetValue(name, out var entityTemplate))
            {
                template = (entityTemplate.Type, entityTemplate.Setup);
                return true;
            }
            
            template = default;
            return false;
        }

        public IEnumerable<string> GetAvailableTemplates()
        {
            return _templates.Keys;
        }
    }
} 