using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages chunk data storage and retrieval.
/// No visualization or generation logic - pure data management.
/// </summary>
public class ChunkManager : MonoBehaviour, IWorldSystem
{
    private Dictionary<Vector3Int, ChunkData> chunks = new Dictionary<Vector3Int, ChunkData>();
    private HashSet<Vector3Int> activeChunks = new HashSet<Vector3Int>();

    public void Initialize()
    {
        chunks.Clear();
        activeChunks.Clear();
    }

    public void Cleanup()
    {
        // Save any dirty chunks before cleanup
        foreach (var chunk in chunks.Values)
        {
            if (chunk.IsDirty)
            {
                SaveChunk(chunk);
            }
        }
        chunks.Clear();
        activeChunks.Clear();
    }

    public ChunkData GetChunkData(Vector3Int position)
    {
        if (chunks.TryGetValue(position, out ChunkData chunk))
        {
            return chunk;
        }
        return null;
    }

    public void StoreChunkData(Vector3Int position, ChunkData chunk)
    {
        chunks[position] = chunk;
        activeChunks.Add(position);
    }

    public void MarkChunkInactive(Vector3Int position)
    {
        activeChunks.Remove(position);
        
        // If chunk is dirty, save it
        if (chunks.TryGetValue(position, out ChunkData chunk) && chunk.IsDirty)
        {
            SaveChunk(chunk);
        }
    }

    private void SaveChunk(ChunkData chunk)
    {
        // TODO: Implement chunk saving to disk
        chunk.IsDirty = false;
    }
} 