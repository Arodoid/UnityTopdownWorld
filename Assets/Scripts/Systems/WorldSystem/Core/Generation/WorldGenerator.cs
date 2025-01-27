using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using System;
using System.Collections.Generic;
using WorldSystem.Core;  // Keep this for Core types
using WorldSystem.Data; // Keep this for Data types

namespace WorldSystem.Generation
{
    public class WorldGenerator : IDisposable
    {
        private readonly FastNoise _noise;
        private readonly Dictionary<int2, bool> _generatingChunks = new();
        public int seed { get; private set; } = 1337;
        private const float WATER_LEVEL = 120f; // Default water level
        
        public NativeArray<BiomeSettings> BiomesArray;

        public WorldGenerator(WorldSystem.WorldGenSettings settings)
        {
            seed = settings.Seed;
            _noise = FastNoise.FromEncodedNodeTree("HgATAB+Faz8bAB0AHgAeACEAIgBmZhZBexSuPhkAHwAgABMACtcjPxkAFwAAAAAAAACAPwrXo78AAABAGgAAAACAPwEkAAIAAAAPAAEAAAAAAABADQAIAAAAAAAAQAcAAAAAAD8AAAAAAAAAAAA/AAAAAAABDQACAAAArkcBQBoAARMAPQrXPv//BQAAKVwvQADXo3A/AIXr0cAA16OwPwBmZrZBAJqZmT4ASOGaQAEXAAAAgL8AAIA/7FE4vs3MzD0bABMAMzPzPxYAAQAAAB8AIAAXAArXoz2PwvU9AAAAAAAAgD///wQAAM3MTD4ArkeRQAAK1yO+AHsUrkAArkfhPv//FAABDQACAAAACtejPhsACAAAKVyPPwDsUTg/AFK4BkEBGQAbAP//GQAArkfhQAAfhWu/ASEADwAEAAAAKVwPQAsAAQAAAAAAAAAAAAAAAwAAAABmZqY/ABSuRz8AcT0KQBcApHC9PwAAwD8pXA8+CtejPiUAKVyPvnsULj8K1+NASOH6PwUAAQAAAAAAAAAK1yM9AAAAAAAAAAAAAAAAPwD2KFw/ANejcD8AzczMvQ==");

            // Initialize default biomes
            BiomesArray = new NativeArray<BiomeSettings>(4, Allocator.Persistent);
            
            // Beach
            BiomesArray[0] = new BiomeSettings 
            { 
                PreferredTemperature = 0.5f,
                PreferredHumidity = 0.5f,
                PreferredContinentalness = 0.45f, // Lowest elevation
                TopBlock = BlockType.Sand,
                UnderwaterBlock = BlockType.Sand
            };

            // Grassland
            BiomesArray[1] = new BiomeSettings 
            { 
                PreferredTemperature = 0.5f,
                PreferredHumidity = 0.5f,
                PreferredContinentalness = 0.57f, // Low-medium elevation
                TopBlock = BlockType.Grass,
                UnderwaterBlock = BlockType.Dirt
            };

            // Mountain
            BiomesArray[2] = new BiomeSettings 
            { 
                PreferredTemperature = 0.5f,
                PreferredHumidity = 0.5f,
                PreferredContinentalness = 0.60f, // Medium-high elevation
                TopBlock = BlockType.Stone,
                UnderwaterBlock = BlockType.Stone
            };

            // Snow
            BiomesArray[3] = new BiomeSettings 
            { 
                PreferredTemperature = 0.5f,
                PreferredHumidity = 0.5f,
                PreferredContinentalness = 0.62f, // Highest elevation
                TopBlock = BlockType.Snow,
                UnderwaterBlock = BlockType.Ice
            };
        }

        public bool IsGenerating(int2 position)
        {
            return _generatingChunks.ContainsKey(position) && _generatingChunks[position];
        }

        public void GenerateChunk(int3 chunkPos, Action<Data.ChunkData> callback)
        {
            var blocks = new NativeArray<byte>(
                Data.ChunkData.SIZE * Data.ChunkData.SIZE * Data.ChunkData.HEIGHT, 
                Allocator.Persistent);
            var heightMap = new NativeArray<Core.HeightPoint>(
                Data.ChunkData.SIZE * Data.ChunkData.SIZE, 
                Allocator.Persistent);

            try
            {
                var job = new TerrainGenerationJob
                {
                    ChunkPosition = chunkPos,
                    Blocks = blocks,
                    HeightMap = heightMap,
                    NoiseValues = new NativeArray<float>(
                        Data.ChunkData.SIZE * Data.ChunkData.SIZE, 
                        Allocator.TempJob)
                };

                job.Schedule(Data.ChunkData.SIZE * Data.ChunkData.SIZE, 64).Complete();

                var dataHeightMap = new NativeArray<Data.HeightPoint>(heightMap.Length, Allocator.Persistent);
                for (int i = 0; i < heightMap.Length; i++)
                {
                    dataHeightMap[i] = new Data.HeightPoint
                    {
                        height = heightMap[i].height,
                        blockType = heightMap[i].blockType
                    };
                }

                var chunkData = new Data.ChunkData
                {
                    position = chunkPos,
                    blocks = blocks,
                    heightMap = dataHeightMap,
                    isEdited = false
                };

                heightMap.Dispose();
                callback(chunkData);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error generating chunk: {e}");
                if (blocks.IsCreated) blocks.Dispose();
                if (heightMap.IsCreated) heightMap.Dispose();
                throw;
            }
        }

        public JobHandle GenerateChunkAsync(int3 chunkPos, NativeArray<byte> blocks, 
            NativeArray<Core.HeightPoint> heightMap, Action<Data.ChunkData> callback)
        {
            var int2Pos = new int2(chunkPos.x, chunkPos.z);
            _generatingChunks[int2Pos] = true;

            var noiseValues = new NativeArray<float>(
                Data.ChunkData.SIZE * Data.ChunkData.SIZE, 
                Allocator.TempJob);
            var temperatureMap = new NativeArray<float>(
                Data.ChunkData.SIZE * Data.ChunkData.SIZE, 
                Allocator.TempJob);
            var humidityMap = new NativeArray<float>(
                Data.ChunkData.SIZE * Data.ChunkData.SIZE, 
                Allocator.TempJob);

            for (int x = 0; x < Data.ChunkData.SIZE; x++)
            {
                for (int z = 0; z < Data.ChunkData.SIZE; z++)
                {
                    float worldX = chunkPos.x * Data.ChunkData.SIZE + x;
                    float worldZ = chunkPos.z * Data.ChunkData.SIZE + z;
                    
                    float scale = 0.004f;
                    
                    // Height/Continentalness noise - normalize from [-1,1] to [0,1]
                    float noise = _noise.GenSingle2D(worldX * scale, worldZ * scale, seed);
                    noiseValues[x + z * Data.ChunkData.SIZE] = (noise + 1f) * 0.5f;
                    
                    // Temperature noise (different seed)
                    temperatureMap[x + z * Data.ChunkData.SIZE] = 
                        (_noise.GenSingle2D(worldX * scale, worldZ * scale, seed + 1) + 1f) * 0.5f;
                    
                    // Humidity noise (different seed)
                    humidityMap[x + z * Data.ChunkData.SIZE] = 
                        (_noise.GenSingle2D(worldX * scale, worldZ * scale, seed + 2) + 1f) * 0.5f;
                }
            }

            var job = new TerrainGenerationJob
            {
                ChunkPosition = chunkPos,
                Blocks = blocks,
                HeightMap = heightMap,
                NoiseValues = noiseValues,
                TemperatureMap = temperatureMap,
                HumidityMap = humidityMap,
                Biomes = BiomesArray,
                WaterLevel = WATER_LEVEL
            };

            var handle = job.Schedule(Data.ChunkData.SIZE * Data.ChunkData.SIZE, 64);
            
            handle.Complete();
            noiseValues.Dispose();
            temperatureMap.Dispose();
            humidityMap.Dispose();
            _generatingChunks[int2Pos] = false;
            
            return handle;
        }

        public void Dispose()
        {
            _generatingChunks.Clear();
            if (BiomesArray.IsCreated)
                BiomesArray.Dispose();
        }
    }
}