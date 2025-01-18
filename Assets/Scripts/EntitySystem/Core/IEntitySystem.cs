using UnityEngine;
using System.Collections.Generic;
using EntitySystem.Core;
using EntitySystem.Core.Types;
using EntitySystem.Core.Interfaces;

namespace EntitySystem.Access
{
    public interface IEntitySystem
    {
        // Entity Lifecycle
        long CreateEntity(string entityType, Vector3 position);
        void DestroyEntity(long entityId);
        
        // State/Component Access
        bool HasComponent(long entityId, string componentType);
        void SetComponentValue(long entityId, string componentType, object value);
        T GetComponentValue<T>(long entityId, string componentType);
        
        // Queries
        IReadOnlyList<long> GetEntitiesInRange(Vector3 position, float radius);
        IReadOnlyList<long> GetEntitiesWithComponent(string componentType);
        Vector3 GetEntityPosition(long entityId);
        
        // State Management
        void SetEntityState(long entityId, EntityState state);
    }
}