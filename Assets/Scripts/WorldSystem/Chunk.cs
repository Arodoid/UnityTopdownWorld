using System;
using UnityEngine;

public class Chunk
{
    // Position of the chunk in chunk coordinates (Vector3Int = {x, y, z})
    public Vector3Int Position { get; }

    // 3D array that represents the blocks in the chunk. Could be extended to include metadata.
    private readonly Block[,,] _blocks;

    // Track if the chunk needs to be re-rendered
    private bool _isDirty = true;

    // Dimension of the chunk in blocks (e.g., 16x16x16)
    public const int ChunkSize = 16;

    private bool? _isOpaque = null; // null = not calculated yet
    private bool? _isEmpty = null;  // Also cache isEmpty for performance

    // Constructor: initializes the chunk at the specified chunk position
    public Chunk(Vector3Int position)
    {
        Position = position;
        _blocks = new Block[ChunkSize, ChunkSize, ChunkSize];
        InitializeBlocks(); // Optional initialization logic        
    }

    /// <summary>
    /// Initializes the blocks to default values (e.g., air, represented as `null`).
    /// Extend this method for custom initialization like procedural terrain generation.
    /// </summary>
    private void InitializeBlocks()
    {
        for (int x = 0; x < ChunkSize; x++)
        {
            for (int y = 0; y < ChunkSize; y++)
            {
                for (int z = 0; z < ChunkSize; z++)
                {
                    _blocks[x, y, z] = null;
                }
            }
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
            _blocks[x, y, z] = block;
            // Invalidate cached states
            _isOpaque = null;
            _isEmpty = null;
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
        if (!_isEmpty.HasValue)
        {
            _isEmpty = CalculateIsEmpty();
        }
        return _isEmpty.Value;
    }

    private bool CalculateIsEmpty()
    {
        foreach (var block in _blocks)
        {
            if (block != null)
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
            if (!_isOpaque.HasValue)
            {
                _isOpaque = CalculateOpacity();
            }
            return _isOpaque.Value;
        }
    }

    private bool CalculateOpacity()
    {
        // Check each column from top to bottom
        for (int x = 0; x < ChunkSize; x++)
        for (int z = 0; z < ChunkSize; z++)
        {
            bool columnIsFullyOpaque = true;
            
            // Check each block in this column from top to bottom
            for (int y = ChunkSize - 1; y >= 0; y--)
            {
                Block block = _blocks[x, y, z];
                if (block == null || !block.IsOpaque)
                {
                    columnIsFullyOpaque = false;
                    break;
                }
            }
            
            if (columnIsFullyOpaque)
                return true;  // If any column is fully opaque, the chunk blocks vision
        }
        return false;
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
}