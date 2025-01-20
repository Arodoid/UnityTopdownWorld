using UnityEngine;
using Unity.Mathematics;
using EntitySystem.Data;
using EntitySystem.Core.Components;
using System.Collections.Generic;

namespace EntitySystem.Core
{
    public class Entity : MonoBehaviour
    {
        public int Id { get; private set; }
        public uint Version { get; private set; }
        public EntityType Type { get; private set; }
        
        private int3 _position;
        public int3 Position
        {
            get => _position;
            set
            {
                _position = value;
                transform.position = new Vector3(value.x, value.y, value.z);
            }
        }

        private Dictionary<System.Type, IEntityComponent> _components = new();
        private TickSystem _tickSystem;

        public void Initialize(int id, uint version, EntityType type, int3 position)
        {
            Id = id;
            Version = version;
            Type = type;
            Position = position;
            
            _tickSystem = FindAnyObjectByType<TickSystem>();
            if (_tickSystem == null)
            {
                Debug.LogError("No TickSystem found during Entity initialization!");
            }
            
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            var components = GetComponents<IEntityComponent>();
            foreach (var component in components)
            {
                var componentType = component.GetType();
                _components[componentType] = component;
                component.Initialize(this);
            }
        }

        public new T GetComponent<T>() where T : class, IEntityComponent
        {
            if (_components.TryGetValue(typeof(T), out var component))
            {
                return component as T;
            }
            return null;
        }

        public bool HasComponent<T>() where T : IEntityComponent
        {
            return _components.ContainsKey(typeof(T));
        }

        public T AddComponent<T>() where T : Component, IEntityComponent
        {
            if (HasComponent<T>()) return GetComponent<T>();

            var component = gameObject.AddComponent<T>();
            
            if (component is ITickable tickable && _tickSystem != null)
            {
                _tickSystem.Register(tickable);
            }
            
            _components[typeof(T)] = component;
            (component as IEntityComponent)?.Initialize(this);
            return component;
        }

        public void OnDestroy()
        {
            var tickables = GetComponents<ITickable>();
            foreach (var tickable in tickables)
            {
                if (_tickSystem != null)
                {
                    _tickSystem.Unregister(tickable);
                }
            }
        }
    }
}