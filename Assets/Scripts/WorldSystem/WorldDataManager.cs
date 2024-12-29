using System.Collections.Generic;
using UnityEngine;

public class WorldDataManager : MonoBehaviour
{
    public const int WORLD_HEIGHT_IN_CHUNKS = 16; // Maximum height of the world in chunks
    
    // Tracks loaded chunks by their position
    private readonly Dictionary<Vector3Int, Chunk> _loadedChunks = new Dictionary<Vector3Int, Chunk>();
}