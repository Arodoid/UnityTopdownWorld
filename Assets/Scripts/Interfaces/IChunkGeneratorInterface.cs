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
    /// <returns>A Task containing the newly generated Chunk object.</returns>
    Task<Chunk> GenerateChunk(Vector3Int position);
}