using UnityEngine;

namespace VoxelGame.WorldSystem.Generation
{
    public class BiomeData
    {
        private readonly float[,] temperatureMap;
        private readonly int size;

        public BiomeData(int size)
        {
            this.size = size;
            temperatureMap = new float[size, size];
        }

        public void SetBiomeData(int x, int z, float temperature)
        {
            if (!IsValidPosition(x, z)) return;
            temperatureMap[x, z] = temperature;
        }

        public float GetTemperature(int x, int z)
        {
            return IsValidPosition(x, z) ? temperatureMap[x, z] : 0f;
        }

        public BiomeType GetBiomeType(int x, int z)
        {
            float temperature = GetTemperature(x, z);
            if (temperature > 0.6f) return BiomeType.Desert;
            if (temperature < 0.4f) return BiomeType.Grassland;
            // Smooth transition zone between 0.4 and 0.6
            return temperature > 0.5f ? BiomeType.Desert : BiomeType.Grassland;
        }

        private bool IsValidPosition(int x, int z)
        {
            return x >= 0 && x < size && z >= 0 && z < size;
        }

        public int Size => size;
    }

    public enum BiomeType
    {
        Grassland,
        Desert
    }
}