using Unity.Mathematics;
using System;
using System.Collections.Generic;

namespace WorldSystem.Persistence
{
    [Serializable]
    public class SerializableChunkData
    {
        public int2 position;
        public byte[] blocks;                  // Full chunk data
        public Dictionary<int, byte> modifications; // Only modifications if edited
        public long lastModified;
        public bool isDirty;

        public SerializableChunkData(int2 position)
        {
            this.position = position;
            this.modifications = new Dictionary<int, byte>();
            this.lastModified = DateTime.Now.Ticks;
            this.isDirty = false;
        }
    }
} 