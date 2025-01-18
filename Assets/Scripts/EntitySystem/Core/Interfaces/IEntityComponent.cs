using UnityEngine;
using EntitySystem.Core.Types;

namespace EntitySystem.Core.Interfaces
{
    public interface IEntityComponent
    {
        void Initialize(Entity entity);
        void OnTick();
        void OnDestroy();
        void OnStateChanged(EntityState oldState, EntityState newState);
        void OnPositionChanged(Vector3 oldPosition, Vector3 newPosition);
    }
} 