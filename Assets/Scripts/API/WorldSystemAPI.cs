using UnityEngine;
using Unity.Mathematics;
using System;
using System.Threading.Tasks;
using WorldSystem.Base;
using WorldSystem.Data;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace WorldSystem.API
{
    /// <summary>
    /// Primary interface for interacting with the world generation and management system.
    /// Handles world loading, block manipulation, and terrain queries.
    /// </summary>
    public class WorldSystemAPI
    {
        private readonly ChunkManager _chunkManager;
        private readonly int _seed;

        /// <summary>
        /// Fired when a world is successfully loaded.
        /// </summary>
        /// <remarks>
        /// The string parameter contains the name of the loaded world.
        /// </remarks>
        public event Action<string> OnWorldLoaded;

        /// <summary>
        /// Initializes a new instance of the WorldSystemAPI.
        /// </summary>
        /// <param name="chunkManager">The chunk manager responsible for world chunk management.</param>
        /// <param name="seed">World generation seed that determines terrain generation.</param>
        public WorldSystemAPI(ChunkManager chunkManager, int seed)
        {
            _chunkManager = chunkManager;
            _seed = seed;
        }

        /// <summary>
        /// Loads or creates a world with the specified name.
        /// </summary>
        /// <param name="worldName">The name of the world to load.</param>
        /// <returns>A task that represents the asynchronous load operation. Returns true if successful.</returns>
        /// <remarks>
        /// This method will initialize the chunk manager and begin generating the initial spawn chunks.
        /// The OnWorldLoaded event will be fired upon successful completion.
        /// </remarks>
        public Task<bool> LoadWorld(string worldName)
        {
            try
            {
                var settings = new WorldGenSettings(_seed);
                _chunkManager.Initialize(settings);
                OnWorldLoaded?.Invoke(worldName);
                return Task.FromResult(true);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load world: {e.Message}");
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// Checks if a block at the specified position is solid.
        /// </summary>
        /// <param name="position">The position to check in world space.</param>
        /// <returns>True if the block is solid, false if it's air or outside loaded chunks.</returns>
        /// <remarks>
        /// A solid block is any block that can be collided with and isn't air or water.
        /// </remarks>
        public bool IsBlockSolid(int3 position)
        {
            return IsBlockSolidAsync(position).Result;
        }

        /// <summary>
        /// Gets the block type at the specified position, loading the chunk if necessary.
        /// </summary>
        /// <param name="position">The position in world space.</param>
        /// <returns>A Task containing the BlockType at the position.</returns>
        /// <remarks>
        /// The block retrieval follows this priority order:
        /// 1. Active chunks (currently rendered and in use)
        /// 2. Cached chunk data (in memory but not rendered)
        /// 3. Persisted chunks (saved on disk)
        /// 4. Generated chunks (if no data exists)
        /// 
        /// Memory Management:
        /// - Active chunks: Fully loaded with mesh data
        /// - Cached data: Just block data, no meshes
        /// - Persisted: On disk, loaded when needed
        /// - Generated: Created from seed when no data exists
        /// 
        /// Note: If data isn't found in any cache, this will trigger the full
        /// chunk generation process, which includes mesh generation and chunk activation.
        /// </remarks>
        public async Task<BlockType> GetBlockType(int3 position)
        {
            return (BlockType)await _chunkManager.GetBlockTypeAsync(position);
        }

        /// <summary>
        /// Sets the block type at the specified position, loading the chunk if necessary.
        /// </summary>
        /// <param name="position">The position in world space.</param>
        /// <param name="blockType">The new block type to set.</param>
        /// <returns>A Task that completes when the block is set.</returns>
        public async Task<bool> SetBlock(int3 position, BlockType blockType)
        {
            return await _chunkManager.SetBlockAsync(position, (byte)blockType);
        }

        /// <summary>
        /// Gets the highest visible block at the given coordinates, considering the current view slice.
        /// </summary>
        /// <param name="x">World X coordinate</param>
        /// <param name="z">World Z coordinate</param>
        /// <param name="maxYLevel">Current maximum Y level being viewed</param>
        /// <returns>The Y coordinate of the highest visible block, or -1 if none found</returns>
        public int GetHighestVisibleBlock(int x, int z, int maxYLevel)
        {
            for (int y = maxYLevel; y >= 0; y--)
            {
                var blockType = GetBlockType(new int3(x, y, z)).Result;
                if (blockType != BlockType.Air)
                {
                    return y;
                }
            }
            return -1;
        }

        /// <summary>
        /// Gets the current maximum Y level being viewed
        /// </summary>
        public int GetCurrentViewLevel()
        {
            return _chunkManager.ViewMaxYLevel;
        }

        public async Task<bool> IsBlockSolidAsync(int3 position)
        {
            var blockType = await GetBlockType(position);
            return blockType != BlockType.Air;
        }
    }
} 