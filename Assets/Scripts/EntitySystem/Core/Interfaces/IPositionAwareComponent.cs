using UnityEngine;

namespace EntitySystem.Core.Interfaces
{
    public interface IPositionAwareComponent : IEntityComponent
    {
        void OnPositionChanged(Vector3 oldPosition, Vector3 newPosition);
    }
} 