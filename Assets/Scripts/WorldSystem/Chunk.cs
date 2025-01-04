using System;
using UnityEngine;

public class Chunk
{
    // Position of the chunk in chunk coordinates (Vector3Int = {x, y, z})
    public Vector3Int Position { get; private set; }

    // 3D array that represents the blocks in the chunk. Could be extended to include metadata.
    private readonly Block[,,] _blocks;

    // Track if the chunk needs to be re-rendered
    private bool _isDirty = true;

    // Dimension of the chunk in blocks (e.g., 16x16x16)
    public const int ChunkSize = 16;

    // Replace bool? with direct flags
    private const byte FLAG_EMPTY = 1;
    private const byte FLAG_OPAQUE = 2;
    private const byte FLAG_OPACITY_CALCULATED = 4;
    private byte _flags = FLAG_EMPTY; // Start empty, opacity not calculated

    // Fast flag operations
    private bool HasFlag(byte flag) => (_flags & flag) == flag;
    private void SetFlag(byte flag) => _flags |= flag;
    private void ClearFlag(byte flag) => _flags &= (byte)~flag;

    // Add this field at the top with other fields
    private bool[,] _opaqueColumns; // Cache for column opacity

    // Constructor: initializes the chunk at the specified chunk position
    public Chunk(Vector3Int position)
    {
        Position = position;
        _blocks = new Block[ChunkSize, ChunkSize, ChunkSize];
        _opaqueColumns = new bool[ChunkSize, ChunkSize]; // Initialize the cache
        InitializeBlocks();
    }

    /// <summary>
    /// Initializes the blocks to default values (e.g., air, represented as `null`).
    /// Extend this method for custom initialization like procedural terrain generation.
    /// </summary>
    private void InitializeBlocks()
    {
        // All blocks start null, _isEmpty is already true
        for (int x = 0; x < ChunkSize; x++)
        for (int y = 0; y < ChunkSize; y++)
        for (int z = 0; z < ChunkSize; z++)
        {
            _blocks[x, y, z] = null;
        }
    }

    /// <summary>
    /// Gets or sets a block at the local block position (relative inside the chunk).
    /// </summary>
    public Block GetBlock(int x, int y, int z)
    {
        if (IsValidBlockPosition(x, y, z))
        {
            return _blocks[x, y, z];
        }
        throw new IndexOutOfRangeException($"Block position [{x}, {y}, {z}] is out of range.");
    }

    public void SetBlock(int x, int y, int z, Block block)
    {
        if (IsValidBlockPosition(x, y, z))
        {
            Block oldBlock = _blocks[x, y, z];
            _blocks[x, y, z] = block;

            // Update flags
            if (block != null)
            {
                ClearFlag(FLAG_EMPTY);
                // Only invalidate opacity for the affected column
                _opaqueColumns[x, z] = false;
                ClearFlag(FLAG_OPACITY_CALCULATED);
            }
            else if (oldBlock != null && CheckIsEmpty())
            {
                SetFlag(FLAG_EMPTY);
            }

            MarkDirty();
        }
        else
        {
            throw new IndexOutOfRangeException($"Block position [{x}, {y}, {z}] is out of range.");
        }
    }

    /// <summary>
    /// Checks if a block position is valid (within bounds of the chunk).
    /// </summary>
    private bool IsValidBlockPosition(int x, int y, int z)
    {
        return x >= 0 && x < ChunkSize &&
               y >= 0 && y < ChunkSize &&
               z >= 0 && z < ChunkSize;
    }

    /// <summary>
    /// Returns true if the chunk is fully empty (all blocks are null).
    /// </summary>
    public bool IsFullyEmpty() => HasFlag(FLAG_EMPTY);

    // Only called when we need to recheck after removing a block
    private bool CheckIsEmpty()
    {
        for (int y = 0; y < ChunkSize; y++)
        for (int x = 0; x < ChunkSize; x++)
        for (int z = 0; z < ChunkSize; z++)
        {
            if (_blocks[x, y, z] != null)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Returns true if the chunk is fully opaque when viewed from above.
    /// Each vertical column must have at least one opaque block to block vision.
    /// </summary>
    public bool IsOpaque
    {
        get
        {
            if (HasFlag(FLAG_EMPTY)) return false;
            if (!HasFlag(FLAG_OPACITY_CALCULATED))
            {
                CalculateOpacity();
            }
            return HasFlag(FLAG_OPAQUE);
        }
    }

    private void CalculateOpacity()
    {
        if (HasFlag(FLAG_EMPTY))
        {
            ClearFlag(FLAG_OPAQUE);
            SetFlag(FLAG_OPACITY_CALCULATED);
            return;
        }

        // Cache local variables to avoid repeated lookups
        var blocks = _blocks;
        var columns = _opaqueColumns;
        int size = ChunkSize;
        bool isFullyOpaque = true;

        for (int x = 0; x < size && isFullyOpaque; x++)
        {
            for (int z = 0; z < size && isFullyOpaque; z++)
            {
                bool hasOpaqueBlock = false;
                
                // Scan from top to bottom with early exit
                for (int y = size - 1; y >= 0; y--)
                {
                    var block = blocks[x, y, z];
                    if (block != null && block.IsOpaque)
                    {
                        hasOpaqueBlock = true;
                        break;
                    }
                }
                
                columns[x, z] = hasOpaqueBlock;
                if (!hasOpaqueBlock)
                {
                    isFullyOpaque = false;
                }
            }
        }

        if (isFullyOpaque)
        {
            SetFlag(FLAG_OPAQUE | FLAG_OPACITY_CALCULATED);
        }
        else
        {
            ClearFlag(FLAG_OPAQUE);
            SetFlag(FLAG_OPACITY_CALCULATED);
        }
    }

    /// <summary>
    /// Marks the chunk as dirty, meaning it needs a mesh rebuild.
    /// </summary>
    public void MarkDirty()
    {
        _isDirty = true;
    }

    /// <summary>
    /// Checks if the chunk is marked dirty (needs a mesh rebuild).
    /// </summary>
    public bool IsDirty()
    {
        return _isDirty;
    }

    /// <summary>
    /// Resets the dirty state to indicate the chunk has been processed.
    /// </summary>
    public void ClearDirty()
    {
        _isDirty = false;
    }

    public void Initialize()
    {
        _flags = FLAG_EMPTY; // Reset all flags
        _isDirty = true;
    }

    public void SetPosition(Vector3Int position)
    {
        Position = position;
    }

    public void ClearBlocks()
    {
        InitializeBlocks();
        _flags = FLAG_EMPTY;
        _isDirty = true;
    }

    public byte[] GetBlocks()
    {
        byte[] blockData = new byte[ChunkSize * ChunkSize * ChunkSize];
        int index = 0;
        var allBlocks = Block.Types.GetAllBlocks();
        
        for (int x = 0; x < ChunkSize; x++)
        for (int y = 0; y < ChunkSize; y++)
        for (int z = 0; z < ChunkSize; z++)
        {
            Block block = _blocks[x, y, z];
            if (block == null)
            {
                blockData[index++] = 0; // Air
            }
            else
            {
                // Find the block type index (1-based)
                byte blockId = (byte)(System.Array.IndexOf(allBlocks, block) + 1);
                blockData[index++] = blockId;
            }
        }
        
        return blockData;
    }
}