using UnityEngine;

namespace VoxelGame.WorldSystem.Biomes
{
    public class BiomeData
    {
        private readonly float[,] temperatureMap;
        private readonly BiomeType[,] biomeTypeMap;
        private readonly int size;

        public BiomeData(int size)
        {
            this.size = size;
            temperatureMap = new float[size, size];
            biomeTypeMap = new BiomeType[size, size];
        }

        public void SetData(int x, int z, float temperature, BiomeType biomeType)
        {
            if (!IsValidPosition(x, z)) return;
            temperatureMap[x, z] = temperature;
            biomeTypeMap[x, z] = biomeType;
        }

        public float GetTemperature(int x, int z)
        {
            return IsValidPosition(x, z) ? temperatureMap[x, z] : 0f;
        }

        public BiomeType GetBiomeType(int x, int z)
        {
            return IsValidPosition(x, z) ? biomeTypeMap[x, z] : BiomeType.Grassland;
        }

        private bool IsValidPosition(int x, int z)
        {
            return x >= 0 && x < size && z >= 0 && z < size;
        }

        public int Size => size;
    }
}