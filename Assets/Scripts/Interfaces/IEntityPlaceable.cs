using UnityEngine;

namespace VoxelGame.Entities
{
    /// <summary>
    /// Interface defining requirements for any entity that can be placed in the world.
    /// Ensures consistent placement validation across different entity types.
    /// </summary>
    public interface IEntityPlaceable
    {
        /// <summary>
        /// Checks if the entity can be placed at the specified position
        /// </summary>
        /// <param name="position">World position to check</param>
        /// <returns>True if placement is valid</returns>
        bool CanPlaceAt(Vector3Int position);

        /// <summary>
        /// Gets the required space for this entity
        /// </summary>
        /// <returns>Vector3Int representing size in each dimension</returns>
        Vector3Int GetRequiredSpace();

        /// <summary>
        /// Gets the entity type for this placeable
        /// </summary>
        EntityType GetEntityType();
    }
} 