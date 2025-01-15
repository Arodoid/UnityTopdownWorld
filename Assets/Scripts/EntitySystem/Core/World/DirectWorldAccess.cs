using Unity.Mathematics;
using WorldSystem.Base;
using WorldSystem.Data;

namespace EntitySystem.Core.World
{
    public class DirectWorldAccess
    {
        private readonly ChunkManager _chunkManager;
        private const int CHUNK_SIZE = 32;
        private const int WORLD_HEIGHT = 256;
        
        // Cache for performance
        private ChunkData _cachedChunk;
        private int2 _cachedChunkPos;

        public DirectWorldAccess(ChunkManager chunkManager)
        {
            _chunkManager = chunkManager;
        }

        public bool IsBlockSolid(int3 position)
        {
            byte blockType = GetBlockType(position);
            return blockType != (byte)BlockType.Air && blockType != (byte)BlockType.Water;
        }

        public byte GetBlockType(int3 position)
        {
            // Quick bounds check
            if (position.y < 0) return (byte)BlockType.Stone;
            if (position.y >= WORLD_HEIGHT) return (byte)BlockType.Air;

            // Fast bit-shift for chunk position (>> 5 is same as / 32)
            int2 chunkPos = new int2(position.x >> 5, position.z >> 5);

            // Use cached chunk if possible
            ChunkData chunk;
            if (_cachedChunk.blocks.IsCreated && _cachedChunkPos.Equals(chunkPos))
            {
                chunk = _cachedChunk;
            }
            else
            {
                chunk = _chunkManager.GetChunk(chunkPos);
                if (chunk.blocks.Length == 0) return (byte)BlockType.Air;
                
                _cachedChunk = chunk;
                _cachedChunkPos = chunkPos;
            }

            // Fast local coordinates (& 31 is same as % 32 for power-of-two)
            int localX = position.x & 31;
            int localZ = position.z & 31;
            int index = localX + (localZ * CHUNK_SIZE) + (position.y * CHUNK_SIZE * CHUNK_SIZE);

            return chunk.blocks[index];
        }

        public bool CanStandAt(int3 position)
        {
            bool feetClear = !IsBlockSolid(position);
            bool headClear = !IsBlockSolid(position + new int3(0, 1, 0));
            bool hasGround = IsBlockSolid(position + new int3(0, -1, 0));
            
            return feetClear && headClear && hasGround;
        }

        public int GetHighestSolidBlock(int x, int z)
        {
            int2 chunkPos = new int2(x >> 5, z >> 5);
            
            // Try to use cached chunk
            ChunkData chunk;
            if (_cachedChunk.blocks.IsCreated && _cachedChunkPos.Equals(chunkPos))
            {
                chunk = _cachedChunk;
            }
            else
            {
                chunk = _chunkManager.GetChunk(chunkPos);
                if (chunk.blocks.Length == 0) return -1;
                
                _cachedChunk = chunk;
                _cachedChunkPos = chunkPos;
            }

            // Get local coordinates
            int localX = x & 31;
            int localZ = z & 31;

            // Use heightmap if available
            if (chunk.heightMap.IsCreated)
            {
                int heightMapIndex = localX + (localZ * CHUNK_SIZE);
                return chunk.heightMap[heightMapIndex].height;
            }

            // Fallback to scanning from top
            for (int y = WORLD_HEIGHT - 1; y >= 0; y--)
            {
                int index = localX + (localZ * CHUNK_SIZE) + (y * CHUNK_SIZE * CHUNK_SIZE);
                if (chunk.blocks[index] != (byte)BlockType.Air)
                {
                    return y;
                }
            }
            return -1;
        }

        public ChunkManager GetChunkManager() => _chunkManager;
    }
} 