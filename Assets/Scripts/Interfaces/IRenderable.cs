using UnityEngine;

namespace VoxelGame.Interfaces
{
    /// <summary>
    /// Represents an object that can be rendered in the 3D world.
    /// </summary>
    public interface IRenderable
    {
        /// <summary>
        /// Returns the 3D mesh to be used for rendering.
        /// </summary>
        /// <returns>A compiled Unity Mesh object.</returns>
        Mesh GetRenderMesh();

        /// <summary>
        /// Returns the position of the renderable object in world space.
        /// </summary>
        /// <returns>The position as a Vector3.</returns>
        Vector3 GetPosition();
    }
} 