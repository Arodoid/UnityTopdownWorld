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

    private bool isChunkOpaque;
    private bool isEmpty = true;
    private bool isEmptyChecked = false;
    
    public ChunkData(Vector3Int position)
    {
        Position = position;
        blocks = new Dictionary<Vector3Int, byte>();
        IsGenerated = false;
        IsDirty = false;
        isChunkOpaque = false;
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
            isEmptyChecked = false;
        }
        else
        {
            blocks[localPosition] = blockType;
            isEmpty = false;
            isEmptyChecked = true;
        }
        
        UpdateChunkOpacity();
        IsDirty = true;
    }

    private void UpdateChunkOpacity()
    {
        // Start assuming chunk is opaque and empty
        isChunkOpaque = true;
        isEmpty = true;
        
        // Check each column
        for (int x = 0; x < CHUNK_SIZE; x++)
        {
            for (int z = 0; z < CHUNK_SIZE; z++)
            {
                bool hasBlockInColumn = false;
                
                // Check each block in this column
                for (int y = 0; y < CHUNK_SIZE; y++)
                {
                    Vector3Int pos = new Vector3Int(x, y, z);
                    byte blockType = GetBlock(pos);
                    
                    if (!IsBlockTransparent(blockType))
                    {
                        hasBlockInColumn = true;
                        isEmpty = false;  // Found a solid block, not empty
                        break;
                    }
                }
                
                // If any column is fully transparent, chunk isn't opaque
                if (!hasBlockInColumn)
                {
                    isChunkOpaque = false;
                }
            }
        }
    }

    private bool IsBlockTransparent(byte blockType)
    {
        return blockType == BlockType.Air.ID;
    }

    public bool IsChunkOpaque()
    {
        return isChunkOpaque;
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

    public bool IsEmpty()
    {
        if (!isEmptyChecked)
        {
            isEmpty = blocks.Count == 0;
            isEmptyChecked = true;
        }
        return isEmpty;
    }

    public void MarkEmpty()
    {
        isEmpty = true;
        isEmptyChecked = true;
    }

    public void MarkNotEmpty()
    {
        isEmpty = false;
        isEmptyChecked = true;
    }
} 