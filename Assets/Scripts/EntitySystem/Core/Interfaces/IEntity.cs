using UnityEngine;
using System.Collections.Generic;
using EntitySystem.Core.Types;

namespace EntitySystem.Core.Interfaces
{
    public interface IEntity
    {
        long Id { get; }
        Vector3 Position { get; }
        EntityState State { get; }
        bool IsActive { get; }
        GameObject GameObject { get; }
        
        void Initialize(GameObject gameObject);
        void OnTick();
        void OnDestroy();
        void SetState(EntityState newState);
        
        T AddComponent<T>() where T : class, IEntityComponent, new();
        T GetComponent<T>() where T : class, IEntityComponent;
        bool HasComponent<T>() where T : class, IEntityComponent;
        void RemoveComponent<T>() where T : class, IEntityComponent;
        IReadOnlyList<IEntityComponent> GetComponents();
    }
} 