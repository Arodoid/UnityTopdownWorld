using System.Collections.Generic;
using UnityEngine;

public class WorldDataManager : MonoBehaviour
{
    public const int WORLD_HEIGHT_IN_CHUNKS = 16; // Maximum height of the world in chunks
    public const int CHUNK_SIZE = 16; // Size of each chunk
    
    // Derived constants
    public const int WORLD_MIN_Y = 0;
    public const int WORLD_MAX_Y = WORLD_HEIGHT_IN_CHUNKS * CHUNK_SIZE; // 256 blocks high
    
    // Tracks loaded chunks by their position
    private readonly Dictionary<Vector3Int, Chunk> _loadedChunks = new Dictionary<Vector3Int, Chunk>();

    public bool IsValidWorldHeight(int y)
    {
        return y >= WORLD_MIN_Y && y < WORLD_MAX_Y;
    }

    public bool IsValidChunkHeight(int chunkY)
    {
        return chunkY >= 0 && chunkY < WORLD_HEIGHT_IN_CHUNKS;
    }
}