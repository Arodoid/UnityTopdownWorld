using UnityEngine;

namespace VoxelGame.Entities
{
    /// <summary>
    /// Defines the basic types of entities that can exist in the game world.
    /// Each type may have different handling for physics, rendering, and behavior.
    /// </summary>
    public enum EntityType
    {
        Static,     // Immobile objects like trees, rocks
        DroppedItem,// Items that can be picked up
        NPC,        // Non-player characters
        Player,     // Player entity
        Structure   // Player-built or generated structures
    }
} 