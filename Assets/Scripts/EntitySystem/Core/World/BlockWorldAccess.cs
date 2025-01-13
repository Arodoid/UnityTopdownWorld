using Unity.Mathematics;
using WorldSystem.Base;
using WorldSystem.Data;
using WorldSystem.Core;
using UnityEngine;
using ChunkData = WorldSystem.Data.ChunkData;
using math = Unity.Mathematics;
using System;

namespace EntitySystem.Core.World
{
    public class BlockWorldAccess : IWorldAccess
    {
        private readonly ChunkManager _chunkManager;
        private const int CHUNK_SIZE = 32;
        private const int WORLD_HEIGHT = 256;

        public BlockWorldAccess(ChunkManager chunkManager)
        {
            _chunkManager = chunkManager;
        }

        public bool IsBlockSolid(int3 position)
        {
            try 
            {
                if (position.y < 0)
                {
                    return true;
                }
                if (position.y >= WORLD_HEIGHT)
                {
                    return false;
                }

                ChunkData chunk = _chunkManager.GetChunk(WorldToChunkPos(position));
                if (ReferenceEquals(chunk, null))
                {
                    return false;
                }
                
                if (chunk.blocks == null || chunk.blocks.Length == 0)
                {
                    return false;
                }

                // Calculate local coordinates correctly
                int localX = position.x >= 0 ? position.x % CHUNK_SIZE : ((position.x + 1) % CHUNK_SIZE) + (CHUNK_SIZE - 1);
                int localZ = position.z >= 0 ? position.z % CHUNK_SIZE : ((position.z + 1) % CHUNK_SIZE) + (CHUNK_SIZE - 1);
                
                // Use the same index calculation as GetBlockType
                int index = localX + (localZ * CHUNK_SIZE) + (position.y * CHUNK_SIZE * CHUNK_SIZE);

                if (index < 0 || index >= chunk.blocks.Length)
                {
                  return false;
                }

                byte blockType = chunk.blocks[index];
                bool isSolid = blockType != (byte)BlockType.Air && blockType != (byte)BlockType.Water;
                
                
                return isSolid;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error checking block at {position}: {e.Message}\n{e.StackTrace}");
                return false;
            }
        }

        public byte GetBlockType(int3 position)
        {
            try 
            {
                if (position.y < 0) return (byte)BlockType.Stone;
                if (position.y >= WORLD_HEIGHT) return (byte)BlockType.Air;

                ChunkData chunk = _chunkManager.GetChunk(WorldToChunkPos(position));
                if (ReferenceEquals(chunk, null) || chunk.blocks == null || chunk.blocks.Length == 0)
                {
                    Debug.LogWarning($"No chunk data at {position}, returning Air");
                    return (byte)BlockType.Air;
                }

                // Calculate local coordinates correctly
                int localX = position.x >= 0 ? position.x % CHUNK_SIZE : ((position.x + 1) % CHUNK_SIZE) + (CHUNK_SIZE - 1);
                int localZ = position.z >= 0 ? position.z % CHUNK_SIZE : ((position.z + 1) % CHUNK_SIZE) + (CHUNK_SIZE - 1);

                // The correct index calculation for a column-major layout
                int index = localX + (localZ * CHUNK_SIZE) + (position.y * CHUNK_SIZE * CHUNK_SIZE);

                if (index < 0 || index >= chunk.blocks.Length)
                {
                    Debug.LogError($"Invalid block index {index} at {position} (local: {localX},{localZ})");
                    return (byte)BlockType.Air;
                }

                byte blockType = chunk.blocks[index];
                return blockType;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error getting block type at {position}: {e.Message}");
                return (byte)BlockType.Air;
            }
        }

        public bool CanStandAt(int3 position)
        {
            // Debug the check
            bool feetClear = !IsBlockSolid(position);
            bool headClear = !IsBlockSolid(position + new int3(0, 1, 0));
            bool hasGround = IsBlockSolid(position + new int3(0, -1, 0));
            
            return feetClear && headClear && hasGround;
        }

        public int GetHighestSolidBlock(int x, int z)
        {
            for (int y = WORLD_HEIGHT - 1; y >= 0; y--)
            {
                if (IsBlockSolid(new int3(x, y, z)))
                {
                    return y;
                }
            }
            return -1; // No solid block found
        }

        private int2 WorldToChunkPos(int3 worldPos)
        {
            // Convert world coordinates to chunk coordinates
            int chunkX = worldPos.x >= 0 ? worldPos.x >> 5 : (worldPos.x - 31) >> 5;
            int chunkZ = worldPos.z >= 0 ? worldPos.z >> 5 : (worldPos.z - 31) >> 5;
            
            return new int2(chunkX, chunkZ);
        }

        public ChunkManager GetChunkManager() => _chunkManager;
    }
} 