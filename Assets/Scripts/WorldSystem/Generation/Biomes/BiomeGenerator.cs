using UnityEngine;
using VoxelGame.WorldSystem.Generation.Core;
using System.Collections.Generic;

namespace VoxelGame.WorldSystem.Biomes
{
    public class BiomeGenerator : MonoBehaviour
    {
        private BiomeGeneratorCore generatorCore;
        private VisualizationManager visualizer;

        private void Start()
        {
            visualizer = FindAnyObjectByType<VisualizationManager>();
        }

        public void Initialize(NoiseGenerator noiseGenerator)
        {
            generatorCore = new BiomeGeneratorCore(noiseGenerator);
        }

        public BiomeData GenerateChunkBiomeData(Vector3Int chunkPosition)
        {
            var biomeData = generatorCore.GenerateChunkBiomeData(chunkPosition);

            if (visualizer != null)
            {
                BiomeType dominantBiome = GetDominantBiome(biomeData);
                visualizer.ShowColumnOverlay(
                    new Vector2Int(chunkPosition.x, chunkPosition.z),
                    BiomeRegistry.GetBiomeColor(dominantBiome)
                );
            }

            return biomeData;
        }

        private BiomeType GetDominantBiome(BiomeData biomeData) => generatorCore.GetDominantBiome(biomeData);
    }

    public class BiomeGeneratorCore
    {
        private readonly NoiseGenerator noiseGenerator;
        private readonly float temperatureScale = GenerationConstants.Noise.BIOME_SCALE;
        private readonly float borderNoiseScale = GenerationConstants.Noise.BIOME_BORDER_SCALE;
        private readonly float borderNoiseStrength = GenerationConstants.Noise.BIOME_BORDER_STRENGTH;

        public BiomeGeneratorCore(NoiseGenerator noiseGenerator)
        {
            this.noiseGenerator = noiseGenerator;
        }

        public BiomeData GenerateChunkBiomeData(Vector3Int chunkPosition)
        {
            var biomeData = new BiomeData(Chunk.ChunkSize);

            for (int x = 0; x < Chunk.ChunkSize; x++)
            for (int z = 0; z < Chunk.ChunkSize; z++)
            {
                float worldX = chunkPosition.x * Chunk.ChunkSize + x;
                float worldZ = chunkPosition.z * Chunk.ChunkSize + z;

                float temperature = GetTemperature(worldX, worldZ);
                BiomeType biomeType = BiomeRegistry.GetBiomeType(temperature);
                biomeData.SetData(x, z, temperature, biomeType);
            }

            return biomeData;
        }

        private float GetTemperature(float worldX, float worldZ)
        {
            // Get base temperature
            float baseTemp = noiseGenerator.GetNoise(worldX, worldZ, 0, temperatureScale);
            
            // Add border noise
            float borderNoise = noiseGenerator.GetNoise(worldX, worldZ, 4, borderNoiseScale);
            borderNoise = (borderNoise * 2 - 1) * borderNoiseStrength;
            
            // Apply border noise to temperature
            float noisyTemp = baseTemp + borderNoise;
            
            // Clamp to valid range
            return Mathf.Clamp01(noisyTemp);
        }

        public BiomeType GetDominantBiome(BiomeData biomeData)
        {
            Dictionary<BiomeType, int> biomeCounts = new();
            BiomeType dominantBiome = BiomeType.Grassland;
            int maxCount = 0;

            for (int x = 0; x < Chunk.ChunkSize; x++)
            for (int z = 0; z < Chunk.ChunkSize; z++)
            {
                BiomeType biome = biomeData.GetBiomeType(x, z);
                if (!biomeCounts.ContainsKey(biome))
                    biomeCounts[biome] = 0;
                biomeCounts[biome]++;
                
                if (biomeCounts[biome] > maxCount)
                {
                    maxCount = biomeCounts[biome];
                    dominantBiome = biome;
                }
            }

            return dominantBiome;
        }
    }
}