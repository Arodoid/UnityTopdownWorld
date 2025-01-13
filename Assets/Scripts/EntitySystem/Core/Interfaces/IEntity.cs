using UnityEngine;
using System.Collections.Generic;
using EntitySystem.Core.Types;

namespace EntitySystem.Core.Interfaces
{
    public interface IEntity
    {
        long Id { get; }
        Vector3 Position { get; set; }
        EntityState State { get; }
        bool IsActive { get; }
        EntityManager Manager { get; }
        
        T AddComponent<T>() where T : IEntityComponent, new();
        T GetComponent<T>() where T : IEntityComponent;
        bool HasComponent<T>() where T : IEntityComponent;
        void RemoveComponent<T>() where T : IEntityComponent;
        
        void Initialize(GameObject gameObject);
        void OnTick();
        void OnDestroy();
        
        void SetState(EntityState newState);
        void UpdatePosition(Vector3 newPosition);
        IReadOnlyList<IEntityComponent> GetComponents();
    }
} 