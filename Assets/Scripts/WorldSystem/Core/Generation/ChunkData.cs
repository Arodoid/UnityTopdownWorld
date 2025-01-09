using Unity.Mathematics;
using Unity.Collections;

namespace WorldSystem.Core
{
    public struct HeightPoint
    {
        public byte height;
        public byte blockType;
    }

    public struct ChunkData
    {
        public const int SIZE = 32;
        public const int HEIGHT = 256;
        
        public int3 position;
        public NativeArray<byte> blocks;
        public NativeArray<HeightPoint> heightMap;
        public bool isEdited;
    }
} 