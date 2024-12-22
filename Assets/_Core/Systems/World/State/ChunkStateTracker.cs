using UnityEngine;
using System.Collections.Generic;

public class ChunkStateTracker : IWorldSystem
{
    private Dictionary<Vector3Int, ChunkState> chunkStates = new Dictionary<Vector3Int, ChunkState>();
    private Dictionary<Vector3Int, float> lastStateChangeTime = new Dictionary<Vector3Int, float>();

    public void Initialize() 
    {
        chunkStates.Clear();
        lastStateChangeTime.Clear();
    }

    public void Cleanup()
    {
        chunkStates.Clear();
        lastStateChangeTime.Clear();
    }

    public ChunkState GetChunkState(Vector3Int pos) 
    {
        return chunkStates.TryGetValue(pos, out ChunkState state) ? state : ChunkState.Unloaded;
    }

    public void SetChunkState(Vector3Int pos, ChunkState newState) 
    {
        if (GetChunkState(pos) != newState)
        {
            chunkStates[pos] = newState;
            lastStateChangeTime[pos] = Time.time;
            Debug.Log($"Chunk {pos} state changed to {newState}");
        }
    }

    public float GetTimeInState(Vector3Int pos)
    {
        return lastStateChangeTime.TryGetValue(pos, out float time) 
            ? Time.time - time 
            : float.MaxValue;
    }

    public bool IsChunkLoaded(Vector3Int pos)
    {
        var state = GetChunkState(pos);
        return state == ChunkState.Active || state == ChunkState.Hidden;
    }

    public bool IsChunkProcessing(Vector3Int pos)
    {
        var state = GetChunkState(pos);
        return state == ChunkState.Queued || 
               state == ChunkState.Generating || 
               state == ChunkState.Meshing;
    }

    public HashSet<Vector3Int> GetChunksInState(ChunkState state)
    {
        HashSet<Vector3Int> chunks = new HashSet<Vector3Int>();
        foreach (var kvp in chunkStates)
        {
            if (kvp.Value == state)
            {
                chunks.Add(kvp.Key);
            }
        }
        return chunks;
    }
} 