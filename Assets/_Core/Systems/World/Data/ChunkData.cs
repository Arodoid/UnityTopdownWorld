using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Pure data structure for storing chunk block data.
/// Uses sparse storage to save memory by only storing non-air blocks.
/// </summary>
public class ChunkData
{
    public const int CHUNK_SIZE = 16;
    public const int WORLD_HEIGHT = 256;
    
    private readonly Dictionary<Vector3Int, byte> blocks;
    public Vector3Int Position { get; private set; }
    
    public bool IsGenerated { get; set; }
    public bool IsDirty { get; set; }

    public ChunkData(Vector3Int position)
    {
        Position = position;
        blocks = new Dictionary<Vector3Int, byte>();
        IsGenerated = false;
        IsDirty = false;
    }

    public byte GetBlock(Vector3Int localPosition)
    {
        return blocks.TryGetValue(localPosition, out byte blockType) ? blockType : BlockType.Air.ID;
    }

    public void SetBlock(Vector3Int localPosition, byte blockType)
    {
        if (blockType == BlockType.Air.ID)
        {
            blocks.Remove(localPosition);
        }
        else
        {
            blocks[localPosition] = blockType;
        }
        IsDirty = true;
    }

    public bool IsValidPosition(Vector3Int localPosition)
    {
        return localPosition.x >= 0 && localPosition.x < CHUNK_SIZE &&
               localPosition.y >= 0 && localPosition.y < CHUNK_SIZE &&
               localPosition.z >= 0 && localPosition.z < CHUNK_SIZE;
    }

    public IEnumerable<KeyValuePair<Vector3Int, byte>> GetAllBlocks()
    {
        return blocks;
    }
} 