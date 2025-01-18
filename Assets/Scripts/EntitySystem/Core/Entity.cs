using UnityEngine;
using System;
using System.Collections.Generic;
using EntitySystem.Core.Interfaces;
using EntitySystem.Core.Types;
using System.Linq;

namespace EntitySystem.Core
{
    public abstract class Entity : IEntity
    {
        public long Id { get; private set; }
        public Vector3 Position => GameObject?.transform.position ?? Vector3.zero;
        public EntityState State { get; private set; }
        public bool IsActive => State == EntityState.Active;
        
        public GameObject GameObject { get; private set; }
        public EntityManager Manager { get; private set; }

        private readonly Dictionary<Type, IEntityComponent> _components = new();
        private Vector3 _lastPosition;

        protected Entity(long id, EntityManager manager)
        {
            Id = id;
            Manager = manager;
            State = EntityState.Inactive;
        }

        public virtual void Initialize(GameObject gameObject)
        {
            GameObject = gameObject;
            _lastPosition = gameObject.transform.position;
            SetupComponents();
        }

        public T AddComponent<T>() where T : class, IEntityComponent, new()
        {
            var type = typeof(T);
            if (_components.TryGetValue(type, out var existing))
                return (T)existing;

            var component = new T();
            _components[type] = component;
            
            if (GameObject != null)
                component.Initialize(this);

            return component;
        }

        public T GetComponent<T>() where T : class, IEntityComponent
            => _components.TryGetValue(typeof(T), out var component) ? (T)component : null;

        public bool HasComponent<T>() where T : class, IEntityComponent
            => _components.ContainsKey(typeof(T));

        public void RemoveComponent<T>() where T : class, IEntityComponent
        {
            if (_components.Remove(typeof(T), out var component))
                component.OnDestroy();
        }

        public IReadOnlyList<IEntityComponent> GetComponents() 
            => _components.Values.ToList();

        public virtual void OnTick()
        {
            if (!IsActive) return;

            // Check position changes
            var currentPos = GameObject.transform.position;
            if (currentPos != _lastPosition)
            {
                foreach (var component in _components.Values)
                    component.OnPositionChanged(_lastPosition, currentPos);
                _lastPosition = currentPos;
            }

            // Update components
            foreach (var component in _components.Values)
                component.OnTick();
        }

        public virtual void OnDestroy()
        {
            foreach (var component in _components.Values)
                component.OnDestroy();
            
            if (GameObject != null)
                UnityEngine.Object.Destroy(GameObject);
            
            _components.Clear();
        }

        public void SetState(EntityState newState)
        {
            if (State == newState) return;
            
            var oldState = State;
            State = newState;
            
            foreach (var component in _components.Values)
                component.OnStateChanged(oldState, newState);

            if (newState == EntityState.Active)
                Manager.AddToActiveEntities(this);
            else if (oldState == EntityState.Active)
                Manager.RemoveFromActiveEntities(this);
        }

        protected abstract void SetupComponents();
    }
}