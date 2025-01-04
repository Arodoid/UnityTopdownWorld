using UnityEngine;

namespace VoxelGame.Interfaces
{
    /// <summary>
    /// Represents a renderer responsible for managing chunk rendering and visual state.
    /// </summary>
    public interface IChunkRenderer
    {
        /// <summary>
        /// Renders a chunk with the generated mesh.
        /// If the chunk is already rendered, updates the existing mesh.
        /// </summary>
        /// <param name="chunk">The chunk to render.</param>
        /// <param name="mesh">The mesh to visually represent the chunk.</param>
        void RenderChunk(Chunk chunk, Mesh mesh);

        /// <summary>
        /// Removes the rendering state of a chunk.
        /// Handles cleanup and deletion of the render objects.
        /// </summary>
        /// <param name="position">The position of the chunk in chunk coordinates.</param>
        void RemoveChunkRender(Vector3Int position);

        /// <summary>
        /// Checks if a chunk is currently being rendered.
        /// </summary>
        /// <param name="position">The position of the chunk in chunk coordinates.</param>
        /// <returns>True if the chunk is being rendered, false otherwise.</returns>
        bool IsChunkRendered(Vector3Int position);
    }
} 