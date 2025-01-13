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
        public Vector3 Position { get; set; }
        public EntityState State { get; private set; }
        public bool IsActive => State == EntityState.Active;
        
        public GameObject GameObject { get; private set; }
        public EntityManager Manager { get; private set; }

        private readonly Dictionary<Type, IEntityComponent> _components;
        private readonly List<IEntityComponent> _componentsList;
        private bool _isInitialized;

        public Entity(long id, EntityManager manager)
        {
            Id = id;
            Manager = manager;
            State = EntityState.Inactive;
            _components = new Dictionary<Type, IEntityComponent>();
            _componentsList = new List<IEntityComponent>();
        }

        // Explicit interface implementations without redundant constraints
        T IEntity.AddComponent<T>()
        {
            Type componentType = typeof(T);
            
            if (_components.ContainsKey(componentType))
            {
                Debug.LogWarning($"Component of type {componentType.Name} already exists on entity {Id}");
                return (T)_components[componentType];
            }

            var component = new T();
            _components[componentType] = (IEntityComponent)component;
            _componentsList.Add((IEntityComponent)component);
            
            if (_isInitialized)
            {
                ((IEntityComponent)component).Initialize(this);
            }

            return component;
        }

        T IEntity.GetComponent<T>()
        {
            return _components.TryGetValue(typeof(T), out var component) ? 
                (T)component : default;
        }

        bool IEntity.HasComponent<T>()
        {
            return _components.ContainsKey(typeof(T));
        }

        void IEntity.RemoveComponent<T>()
        {
            Type componentType = typeof(T);
            if (_components.TryGetValue(componentType, out var component))
            {
                component.OnDestroy();
                _components.Remove(componentType);
                _componentsList.Remove(component);
            }
        }

        IReadOnlyList<EntitySystem.Core.Interfaces.IEntityComponent> IEntity.GetComponents()
        {
            return _componentsList.Select(c => (EntitySystem.Core.Interfaces.IEntityComponent)c).ToList().AsReadOnly();
        }

        // Public methods with stronger constraints
        public T AddComponent<T>() where T : class, EntitySystem.Core.Interfaces.IEntityComponent, new()
        {
            return ((IEntity)this).AddComponent<T>();
        }

        public T GetComponent<T>() where T : class, EntitySystem.Core.Interfaces.IEntityComponent
        {
            return ((IEntity)this).GetComponent<T>();
        }

        public bool HasComponent<T>() where T : class, EntitySystem.Core.Interfaces.IEntityComponent
        {
            return ((IEntity)this).HasComponent<T>();
        }

        public void RemoveComponent<T>() where T : class, EntitySystem.Core.Interfaces.IEntityComponent
        {
            ((IEntity)this).RemoveComponent<T>();
        }

        public IReadOnlyList<EntitySystem.Core.Interfaces.IEntityComponent> GetComponents()
        {
            return _componentsList.Select(c => (EntitySystem.Core.Interfaces.IEntityComponent)c).ToList().AsReadOnly();
        }

        public virtual void Initialize(GameObject gameObject)
        {
            GameObject = gameObject;
            _isInitialized = true;
            SetupComponents();
        }

        protected abstract void SetupComponents();

        public virtual void OnTick()
        {
            if (!IsActive) return;

            foreach (var component in _componentsList)
            {
                component.OnTick();
            }
        }

        public virtual void OnDestroy()
        {
            foreach (var component in _componentsList)
            {
                component.OnDestroy();
            }
            
            if (GameObject != null)
            {
                UnityEngine.Object.Destroy(GameObject);
            }
            
            _components.Clear();
            _componentsList.Clear();
        }

        public void SetState(EntityState newState)
        {
            if (State == newState) return;
            
            var oldState = State;
            State = newState;
            
            
            foreach (var component in _componentsList)
            {
                (component as IStateAwareComponent)?.OnStateChanged(oldState, newState);
            }

            // Update active entities list if needed
            if (newState == EntityState.Active)
            {
                Manager.RegisterForTicks(this);
            }
            else if (oldState == EntityState.Active)
            {
                Manager.UnregisterFromTicks(this);
            }
        }

        public void UpdatePosition(Vector3 newPosition)
        {
            if (Position == newPosition) return;
            
            var oldPosition = Position;
            Position = newPosition;
            
            if (GameObject != null)
            {
                GameObject.transform.position = newPosition;
            }
            
            foreach (var component in _componentsList)
            {
                (component as IPositionAwareComponent)?.OnPositionChanged(oldPosition, newPosition);
            }
        }
    }
}