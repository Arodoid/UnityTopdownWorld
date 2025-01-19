using UnityEngine;

namespace WorldSystem
{
    public class WorldGenSettings
    {
        public int Seed { get; }

        public WorldGenSettings(int seed)
        {
            Seed = seed;
        }
    }
}