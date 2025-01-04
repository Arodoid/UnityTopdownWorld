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

    // Remove nullable since we'll track it directly
    private bool _isEmpty = true;  // Start empty
    private bool? _isOpaque = null;

    // Constructor: initializes the chunk at the specified chunk position
    public Chunk(Vector3Int position)
    {
        Position = position;
        _blocks = new Block[ChunkSize, ChunkSize, ChunkSize];
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

            // Update emptiness state
            if (block != null)
            {
                _isEmpty = false;
            }
            else if (oldBlock != null)
            {
                // Only need to recheck if we removed a block
                _isEmpty = CheckIsEmpty();
            }

            // Invalidate opacity cache
            _isOpaque = null;
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
    public bool IsFullyEmpty()
    {
        return _isEmpty;
    }

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
            // Short circuit if empty
            if (_isEmpty) return false;

            if (!_isOpaque.HasValue)
            {
                _isOpaque = CalculateOpacity();
            }
            return _isOpaque.Value;
        }
    }

    private bool CalculateOpacity()
    {
        // Skip calculation if chunk is empty
        if (_isEmpty) return false;

        for (int x = 0; x < ChunkSize; x++)
        for (int z = 0; z < ChunkSize; z++)
        {
            bool hasOpaqueBlock = false;
            
            for (int y = ChunkSize - 1; y >= 0; y--)
            {
                Block block = _blocks[x, y, z];
                if (block != null && block.IsOpaque)
                {
                    hasOpaqueBlock = true;
                    break;
                }
            }
            
            if (!hasOpaqueBlock)
                return false;
        }
        return true;
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
        // Reset states
        _isEmpty = true;
        _isOpaque = null;
        _isDirty = true;
    }

    public void SetPosition(Vector3Int position)
    {
        Position = position;
    }

    public void ClearBlocks()
    {
        InitializeBlocks();
        _isEmpty = true;
        _isOpaque = null;
        _isDirty = true;
    }
}