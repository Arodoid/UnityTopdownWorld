using Unity.Mathematics;
using WorldSystem.Base;

namespace EntitySystem.Core.World
{
    public interface IWorldAccess
    {
        bool IsBlockSolid(int3 position);
        byte GetBlockType(int3 position);
        bool CanStandAt(int3 position);
        int GetHighestSolidBlock(int x, int z);
        ChunkManager GetChunkManager();
    }
}