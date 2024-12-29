using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Represents a chunk generator capable of creating chunk data.
/// </summary>
public interface IChunkGenerator
{
    /// <summary>
    /// Generates a chunk at the given chunk position.
    /// </summary>
    /// <param name="position">The position of the chunk in chunk coordinates.</param>
    /// <returns>A newly generated Chunk object.</returns>
    Chunk GenerateChunk(Vector3Int position);

    /// <summary>
    /// Asynchronously generates a chunk at the given chunk position.
    /// </summary>
    /// <param name="position">The position of the chunk in chunk coordinates.</param>
    /// <returns>A Task that resolves to a newly generated Chunk object.</returns>
    Task<Chunk> GenerateChunkAsync(Vector3Int position);
}