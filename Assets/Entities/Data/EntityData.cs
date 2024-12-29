using UnityEngine;
using System;

namespace VoxelGame.Entities
{
    /// <summary>
    /// Core data structure for all entities in the game world.
    /// Contains essential information independent of Unity's GameObject system.
    /// This separation allows for efficient entity management and serialization.
    /// </summary>
    public class EntityData
    {
        // Core identification
        public Guid UUID { get; private set; }
        public EntityType Type { get; private set; }

        // Transform data
        public Vector3Int Position { get; private set; }
        public Quaternion Rotation { get; private set; }

        // State
        public bool IsActive { get; private set; }
        public string SerializedState { get; private set; }

        public EntityData(EntityType type, Vector3Int position, Quaternion rotation)
        {
            UUID = Guid.NewGuid();
            Type = type;
            Position = position;
            Rotation = rotation;
            IsActive = true;
            SerializedState = string.Empty;
        }

        // State modification methods
        public void UpdatePosition(Vector3Int newPosition) => Position = newPosition;
        public void UpdateRotation(Quaternion newRotation) => Rotation = newRotation;
        public void SetActive(bool active) => IsActive = active;
        public void SetState(string state) => SerializedState = state;
    }
}