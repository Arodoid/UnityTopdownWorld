using Unity.Mathematics;
using WorldSystem.Data;

namespace WorldSystem.Generation
{
    public interface IChunkGenerator
    {
        int seed { get; set; }
        void QueueChunkGeneration(int2 position);
        bool IsGenerating(int2 position);
        void Update();
        void Dispose();
        event System.Action<ChunkData> OnChunkGenerated;
    }
} 