using System.Collections.Generic;
using UnityEngine;

public class ChunkManager : MonoBehaviour
{
    // Dictionary to store chunks with their positions as the key
    private readonly Dictionary<Vector3Int, Chunk> _chunks = new Dictionary<Vector3Int, Chunk>();

    // Tracks chunks marked for unloading
    private readonly HashSet<Vector3Int> _chunksMarkedForUnloading = new HashSet<Vector3Int>();

    /// <summary>
    /// Retrieves a chunk at the given position.
    /// </summary>
    /// <param name="position">The position of the chunk in chunk coordinates.</param>
    /// <returns>The chunk at the position, or null if it doesn't exist.</returns>
    public Chunk GetChunk(Vector3Int position)
    {
        _chunks.TryGetValue(position, out var chunk);
        return chunk;
    }

    /// <summary>
    /// Stores a chunk in memory at the specified position.
    /// Overwrites any existing chunk at this position.
    /// </summary>
    /// <param name="position">The position to store the chunk in chunk coordinates.</param>
    /// <param name="chunk">The chunk to be stored.</param>
    public void StoreChunk(Vector3Int position, Chunk chunk)
    {
        _chunks[position] = chunk;

        // If the chunk was marked for unloading before storing, unmark it
        _chunksMarkedForUnloading.Remove(position);
    }

    /// <summary>
    /// Removes a chunk from memory at the specified position.
    /// </summary>
    /// <param name="position">The position of the chunk to remove.</param>
    public void RemoveChunk(Vector3Int position)
    {
        if (_chunks.ContainsKey(position))
        {
            _chunks.Remove(position);
        }
    }

    /// <summary>
    /// Checks if a chunk exists in memory at the specified position.
    /// </summary>
    /// <param name="position">The position of the chunk in chunk coordinates.</param>
    /// <returns>True if the chunk exists, false otherwise.</returns>
    public bool ChunkExists(Vector3Int position)
    {
        return _chunks.ContainsKey(position);
    }

    /// <summary>
    /// Marks a chunk for unloading (removing from memory later).
    /// </summary>
    /// <param name="position">The position of the chunk to mark for unloading.</param>
    public void MarkForUnloading(Vector3Int position)
    {
        if (_chunks.ContainsKey(position))
        {
            _chunksMarkedForUnloading.Add(position);
        }
    }

    /// <summary>
    /// Unloads chunks that were previously marked for unloading.
    /// </summary>
    public void UnloadMarkedChunks()
    {
        foreach (var position in _chunksMarkedForUnloading)
        {
            RemoveChunk(position);
        }

        _chunksMarkedForUnloading.Clear();
    }

    public bool IsMarkedForUnloading(Vector3Int position)
    {
        return _chunksMarkedForUnloading.Contains(position);
    }
}