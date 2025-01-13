using UnityEngine;
using System;
using System.Collections.Generic;

namespace EntitySystem.Core
{
    public class Entity
    {
        public long Id { get; private set; }
        public bool IsActive { get; set; }
        public Vector3 Position { get; set; }
        public GameObject GameObject { get; private set; }
        public EntityManager Manager { get; private set; }

        private Dictionary<Type, IEntityComponent> _components;

        public Entity(long id, EntityManager manager)
        {
            Id = id;
            Manager = manager;
            _components = new Dictionary<Type, IEntityComponent>();
            IsActive = true;
        }

        public T AddComponent<T>() where T : IEntityComponent, new()
        {
            var component = new T();
            _components[typeof(T)] = component;
            component.Initialize(this);
            return component;
        }

        public T GetComponent<T>() where T : IEntityComponent
        {
            return _components.TryGetValue(typeof(T), out var component) ? 
                (T)component : default;
        }

        public bool HasComponent<T>() where T : IEntityComponent
        {
            return _components.ContainsKey(typeof(T));
        }

        public virtual void Initialize(GameObject gameObject)
        {
            GameObject = gameObject;
            Position = gameObject.transform.position;
        }

        public virtual void OnTick()
        {
            foreach (var component in _components.Values)
            {
                component.OnTick();
            }
        }

        public virtual void OnDestroy()
        {
            foreach (var component in _components.Values)
            {
                component.OnDestroy();
            }
            
            if (GameObject != null)
            {
                UnityEngine.Object.Destroy(GameObject);
            }
        }
    }
} 