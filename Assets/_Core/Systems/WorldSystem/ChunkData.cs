using UnityEngine;
using System.Collections.Generic;

public class ChunkData
{
    public const int CHUNK_SIZE = 16;
    public const int WORLD_DEPTH = 3;
    
    private Dictionary<Vector3Int, byte> blocks;
    public Vector3Int ChunkPosition { get; private set; }
    public bool IsGenerated { get; set; }

    // Cache for frequently accessed positions
    private readonly Vector3Int[] neighborOffsets = new Vector3Int[]
    {
        Vector3Int.forward, Vector3Int.back,
        Vector3Int.up, Vector3Int.down,
        Vector3Int.left, Vector3Int.right
    };

    public ChunkData(Vector3Int chunkPos)
    {
        ChunkPosition = chunkPos;
        blocks = new Dictionary<Vector3Int, byte>();
        IsGenerated = false;
    }

    public byte GetBlock(int x, int y, int z)
    {
        var pos = new Vector3Int(x, y, z);
        return blocks.TryGetValue(pos, out byte value) ? value : BlockWorld.AIR;
    }

    public void SetBlock(int x, int y, int z, byte blockType)
    {
        var pos = new Vector3Int(x, y, z);
        if (blockType == BlockWorld.AIR)
            blocks.Remove(pos);
        else
            blocks[pos] = blockType;
    }

    public IEnumerable<KeyValuePair<Vector3Int, byte>> GetNonAirBlocks()
    {
        return blocks;
    }
} 