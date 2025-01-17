using WorldSystem.Implementation;
using WorldSystem.Generation;
using WorldSystem.Base;

namespace WorldSystem
{
    public static class WorldSystemFactory
    {
        public static IWorldSystem CreateWorldSystem(WorldGenSettings settings, ChunkManager chunkManager)
        {
            return new WorldSystemImpl(settings, chunkManager);
        }
    }
} 