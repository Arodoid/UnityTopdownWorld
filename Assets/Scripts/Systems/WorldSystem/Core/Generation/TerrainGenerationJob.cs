using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;
using WorldSystem.Core;
using WorldSystem.Data;
using UnityEngine;
using System.Runtime.CompilerServices;

namespace WorldSystem.Generation
{
    public struct TerrainGenerationJob : IJobParallelFor
    {
        [ReadOnly] public int3 ChunkPosition;
        [NativeDisableParallelForRestriction] public NativeArray<byte> Blocks;
        [NativeDisableParallelForRestriction] public NativeArray<Core.HeightPoint> HeightMap;
        
        // 3D noise for basic structure and caves
        [ReadOnly] public NativeArray<float> NoiseValues3D;
        // 2D noise for height offsets
        [ReadOnly] public NativeArray<float> HeightOffsets;
        
        // Biome data
        [ReadOnly] public NativeArray<float> TemperatureMap;
        [ReadOnly] public NativeArray<float> HumidityMap;
        [ReadOnly] public NativeArray<BiomeSettings> Biomes;
        
        public float WaterLevel;
        public float BaseHeight;    // Y level where the transition happens (in block space)
        public float Strength;      // How sharp the transition is

        public void Execute(int index)
        {
            int x = index % Data.ChunkData.SIZE;
            int z = index / Data.ChunkData.SIZE;
            int mapIndex = x + z * Data.ChunkData.SIZE;
            
            // Scale height offset to world height (0-256)
            float heightOffset = HeightOffsets[mapIndex] * Data.ChunkData.HEIGHT;
            
            for (int y = 0; y < Data.ChunkData.HEIGHT; y++)
            {
                int blockIndex = x + (z * Data.ChunkData.SIZE) + (y * Data.ChunkData.SIZE * Data.ChunkData.SIZE);
                
                // Simple height comparison - if we're below the surface, it's solid
                bool isSolid = y < heightOffset;
                
                // Add some noise near the surface
                if (math.abs(y - heightOffset) < 4)  // 4 blocks of noise
                {
                    float noiseValue = NoiseValues3D[blockIndex];
                    isSolid = noiseValue < 0.5f;
                }
                
                if (isSolid)
                {
                    var selectedBiome = GetBiomeForPosition(x, z);
                    Blocks[blockIndex] = (byte)(y < WaterLevel ? selectedBiome.UnderwaterBlock : selectedBiome.TopBlock);
                }
                else
                {
                    Blocks[blockIndex] = (byte)(y < WaterLevel ? BlockType.Water : BlockType.Air);
                }
            }

            // Update height map
            int height = (int)heightOffset;
            height = math.clamp(height, 0, Data.ChunkData.HEIGHT - 1);
            
            HeightMap[mapIndex] = new Core.HeightPoint
            {
                height = (byte)height,
                blockType = Blocks[x + (z * Data.ChunkData.SIZE) + (height * Data.ChunkData.SIZE * Data.ChunkData.SIZE)]
            };
        }

        private BiomeSettings GetBiomeForPosition(int x, int z)
        {
            int mapIndex = x + z * Data.ChunkData.SIZE;
            
            // Cache these values to avoid multiple array accesses
            float temperature = TemperatureMap[mapIndex];
            float humidity = HumidityMap[mapIndex];
            float continentalness = HeightOffsets[mapIndex];
            
            // Use local variables to avoid struct copies
            int bestBiomeIndex = 0;
            float bestMatch = float.MaxValue;
            
            // Avoid bounds checking by caching length
            int biomesLength = Biomes.Length;
            
            // Manual loop unrolling since we know we typically have 4 biomes
            if (biomesLength >= 4)
            {
                for (int i = 0; i < 4; i++)
                {
                    float match = CalculateBiomeMatch(
                        Biomes[i], 
                        temperature, 
                        humidity, 
                        continentalness);
                        
                    if (match < bestMatch)
                    {
                        bestMatch = match;
                        bestBiomeIndex = i;
                    }
                }
            }
            // Fallback for remaining biomes if we have more than 4
            for (int i = 4; i < biomesLength; i++)
            {
                float match = CalculateBiomeMatch(
                    Biomes[i], 
                    temperature, 
                    humidity, 
                    continentalness);
                    
                if (match < bestMatch)
                {
                    bestMatch = match;
                    bestBiomeIndex = i;
                }
            }
            
            return Biomes[bestBiomeIndex];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float CalculateBiomeMatch(
            BiomeSettings biome, 
            float temperature, 
            float humidity, 
            float continentalness)
        {
            float tempDiff = temperature - biome.PreferredTemperature;
            float humidDiff = humidity - biome.PreferredHumidity;
            float contDiff = continentalness - biome.PreferredContinentalness;
            
            // Avoid multiplication by using addition
            return tempDiff * tempDiff + 
                   humidDiff * humidDiff + 
                   contDiff * contDiff;
        }
    }
}
