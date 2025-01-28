using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using System;
using System.Collections.Generic;
using WorldSystem.Core;  // Keep this for Core types
using WorldSystem.Data; // Keep this for Data types
using WorldSystem.Generation.Features;

namespace WorldSystem.Generation
{
    public class WorldGenerator : IDisposable
    {
        private readonly FastNoise _3dNoise;  // For basic terrain structure
        private readonly FastNoise _heightNoise;  // For height offsets
        private readonly FastNoise _temperatureNoise;  // For biome temperature
        private readonly FastNoise _humidityNoise;  // For biome humidity
        private readonly Dictionary<int2, bool> _generatingChunks = new();
        private readonly WorldSystem.WorldGenSettings _settings;
        public int seed { get; private set; } = 1337;
        private const float WATER_LEVEL = 120f; // Default water level
        
        public NativeArray<BiomeSettings> BiomesArray;

        public WorldGenerator(WorldSystem.WorldGenSettings settings)
        {
            _settings = settings;
            seed = settings.Seed;
            
            // Simple 3D Perlin noise for basic structure
            _3dNoise = FastNoise.FromEncodedNodeTree("BwA=");
            
            // More complex noise for height offsets (your existing noise)
            _heightNoise = FastNoise.FromEncodedNodeTree("HgATAB+Faz8bAB0AHgAeACEAIgBxPQpBmpkZPxkAHwAgABMACtcjPxkAFwAAAAAAAACAPwrXo78AAABAGgAAAACAPwEkAAIAAAAPAAEAAAAAAABADQAIAAAAAAAAQAcAAAAAAD8AAAAAAAAAAAA/AAAAAAABDQACAAAArkcBQBoAARMAPQrXPv//BQAAKVwvQADXo3A/AIXr0cAA16OwPwBmZrZBAJqZmT4ASOGaQAEXAAAAgL8AAIA/7FE4vs3MzD0bABMAMzPzPxYAAQAAAB8AIAAXAArXoz2PwvU9AAAAAAAAgD///wQAAM3MTD4ArkeRQAAK1yO+AHsUrkAA16NwP///FAABDQADAAAAexSuPhsACAAAcT2KPwDsUTg/AFyPskABGQAbAP//GQAA16PQQAAfhWu/ASEADwAEAAAAKVwPQAsAAQAAAAAAAAAAAAAAAwAAAABmZqY/ABSuRz8AcT0KQBcApHC9PwAAwD8pXA8+CtejPiUAKVyPvnsULj8K1+NASOH6PwUAAQAAAAAAAAAK1yM9AAAAAAAAAAAAAAAAPwD2KFw/AKRwPT8AzczMvQ==");
            
            // Simple noise for temperature and humidity
            _temperatureNoise = FastNoise.FromEncodedNodeTree("BwA=");
            _humidityNoise = FastNoise.FromEncodedNodeTree("BwA=");

            // Initialize default biomes
            BiomesArray = new NativeArray<BiomeSettings>(4, Allocator.Persistent);
            
            // Beach
            BiomesArray[0] = new BiomeSettings 
            { 
                PreferredTemperature = 0.5f,
                PreferredHumidity = 0.5f,
                PreferredContinentalness = 0.48f,
                TopBlock = BlockType.Sand,
                UnderwaterBlock = BlockType.Sand,
                AllowsTrees = true,  // Enable trees for beach
                TreeDensity = 0.01f,  // Lower density for palm trees
                TreeMinHeight = 6f,    // Palm trees are typically taller
                TreeMaxHeight = 10f,
                IsPalmTree = true     // New flag to indicate palm trees
            };

            // Grassland
            BiomesArray[1] = new BiomeSettings 
            { 
                PreferredTemperature = 0.5f,
                PreferredHumidity = 0.5f,
                PreferredContinentalness = 0.52f, // Low-medium elevation
                TopBlock = BlockType.Grass,
                UnderwaterBlock = BlockType.Dirt,
                AllowsTrees = true,
                TreeDensity = 0.01f,
                TreeMinHeight = 4f,
                TreeMaxHeight = 8f,
                AllowsRocks = true,
                RockDensity = 0.0005f,
                RockMinSize = 2f,
                RockMaxSize = 4f,
                RockSpikiness = 0.5f,
                RockGroundDepth = 1f
            };

            // Mountain
            BiomesArray[2] = new BiomeSettings 
            { 
                PreferredTemperature = 0.5f,
                PreferredHumidity = 0.5f,
                PreferredContinentalness = 0.58f, // Medium-high elevation
                TopBlock = BlockType.Stone,
                UnderwaterBlock = BlockType.Stone,
                AllowsTrees = false,
                AllowsRocks = true,
                RockDensity = 0.0005f,
                RockMinSize = 2f,
                RockMaxSize = 4f,
                RockSpikiness = 0.5f,
                RockGroundDepth = 1f
            };

            // Snow
            BiomesArray[3] = new BiomeSettings 
            { 
                PreferredTemperature = 0.5f,
                PreferredHumidity = 0.5f,
                PreferredContinentalness = 0.67f, // Highest elevation
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
                // Allocate arrays for noise values
                var noiseValues3D = new NativeArray<float>(
                    Data.ChunkData.SIZE * Data.ChunkData.SIZE * Data.ChunkData.HEIGHT, 
                    Allocator.TempJob);
                var heightOffsets = new NativeArray<float>(
                    Data.ChunkData.SIZE * Data.ChunkData.SIZE, 
                    Allocator.TempJob);
                var temperatureMap = new NativeArray<float>(
                    Data.ChunkData.SIZE * Data.ChunkData.SIZE, 
                    Allocator.TempJob);
                var humidityMap = new NativeArray<float>(
                    Data.ChunkData.SIZE * Data.ChunkData.SIZE, 
                    Allocator.TempJob);

                // Generate noise values (similar to GenerateChunkAsync)
                float scale2D = 0.004f;
                float scale3D = 0.03f;

                // Generate 3D noise
                for (int y = 0; y < Data.ChunkData.HEIGHT; y++)
                {
                    for (int x = 0; x < Data.ChunkData.SIZE; x++)
                    {
                        for (int z = 0; z < Data.ChunkData.SIZE; z++)
                        {
                            float worldX = chunkPos.x * Data.ChunkData.SIZE + x;
                            float worldY = y;
                            float worldZ = chunkPos.z * Data.ChunkData.SIZE + z;

                            int index = x + (z * Data.ChunkData.SIZE) + (y * Data.ChunkData.SIZE * Data.ChunkData.SIZE);
                            float noise = _3dNoise.GenSingle3D(
                                worldX * scale3D, 
                                worldY * scale3D, 
                                worldZ * scale3D, 
                                seed);
                            noiseValues3D[index] = (noise + 1f) * 0.5f;
                        }
                    }
                }

                // Generate 2D noise
                for (int x = 0; x < Data.ChunkData.SIZE; x++)
                {
                    for (int z = 0; z < Data.ChunkData.SIZE; z++)
                    {
                        float worldX = chunkPos.x * Data.ChunkData.SIZE + x;
                        float worldZ = chunkPos.z * Data.ChunkData.SIZE + z;
                        
                        int index = x + z * Data.ChunkData.SIZE;
                        
                        heightOffsets[index] = (_heightNoise.GenSingle2D(worldX * scale2D, worldZ * scale2D, seed) + 1f) * 0.5f;
                        temperatureMap[index] = (_temperatureNoise.GenSingle2D(worldX * scale2D, worldZ * scale2D, seed + 1) + 1f) * 0.5f;
                        humidityMap[index] = (_humidityNoise.GenSingle2D(worldX * scale2D, worldZ * scale2D, seed + 2) + 1f) * 0.5f;
                    }
                }

                var job = new TerrainGenerationJob
                {
                    ChunkPosition = chunkPos,
                    Blocks = blocks,
                    HeightMap = heightMap,
                    NoiseValues3D = noiseValues3D,
                    HeightOffsets = heightOffsets,
                    TemperatureMap = temperatureMap,
                    HumidityMap = humidityMap,
                    Biomes = BiomesArray,
                    BaseHeight = 64f / Data.ChunkData.HEIGHT,  // Normalize to 0-1 range
                    Strength = 1.0f,
                    WaterLevel = WATER_LEVEL,
                };

                job.Schedule(Data.ChunkData.SIZE * Data.ChunkData.SIZE, 64).Complete();

                // Cleanup noise arrays
                noiseValues3D.Dispose();
                heightOffsets.Dispose();
                temperatureMap.Dispose();
                humidityMap.Dispose();

                var dataHeightMap = ConvertHeightMap(heightMap);
                var chunkData = new Data.ChunkData
                {
                    position = chunkPos,
                    blocks = blocks,
                    heightMap = dataHeightMap,
                    isEdited = false
                };

                // Use stored settings
                var featureGen = new FeatureGenerator(_settings);
                featureGen.PopulateChunk(ref chunkData, BiomesArray);

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

            // Allocate arrays for noise values
            var noiseValues3D = new NativeArray<float>(
                Data.ChunkData.SIZE * Data.ChunkData.SIZE * Data.ChunkData.HEIGHT, 
                Allocator.TempJob);
            var heightOffsets = new NativeArray<float>(
                Data.ChunkData.SIZE * Data.ChunkData.SIZE, 
                Allocator.TempJob);
            var temperatureMap = new NativeArray<float>(
                Data.ChunkData.SIZE * Data.ChunkData.SIZE, 
                Allocator.TempJob);
            var humidityMap = new NativeArray<float>(
                Data.ChunkData.SIZE * Data.ChunkData.SIZE, 
                Allocator.TempJob);

            try
            {
                // Generate noise values
                float scale2D = 0.004f;
                float scale3D = 0.03f;  // Adjust this to control cave size

                // Generate 3D noise for basic structure
                for (int y = 0; y < Data.ChunkData.HEIGHT; y++)
                {
                    for (int x = 0; x < Data.ChunkData.SIZE; x++)
                    {
                        for (int z = 0; z < Data.ChunkData.SIZE; z++)
                        {
                            float worldX = chunkPos.x * Data.ChunkData.SIZE + x;
                            float worldY = y;
                            float worldZ = chunkPos.z * Data.ChunkData.SIZE + z;

                            int index = x + (z * Data.ChunkData.SIZE) + (y * Data.ChunkData.SIZE * Data.ChunkData.SIZE);
                            float noise = _3dNoise.GenSingle3D(
                                worldX * scale3D, 
                                worldY * scale3D, 
                                worldZ * scale3D, 
                                seed);
                            noiseValues3D[index] = (noise + 1f) * 0.5f;  // Normalize to 0-1
                        }
                    }
                }

                // Generate 2D noise for height offsets and biome data
                for (int x = 0; x < Data.ChunkData.SIZE; x++)
                {
                    for (int z = 0; z < Data.ChunkData.SIZE; z++)
                    {
                        float worldX = chunkPos.x * Data.ChunkData.SIZE + x;
                        float worldZ = chunkPos.z * Data.ChunkData.SIZE + z;
                        
                        int index = x + z * Data.ChunkData.SIZE;
                        
                        // Height offset noise
                        heightOffsets[index] = (_heightNoise.GenSingle2D(worldX * scale2D, worldZ * scale2D, seed) + 1f) * 0.5f;
                        
                        // Temperature noise (different seed)
                        temperatureMap[index] = (_temperatureNoise.GenSingle2D(worldX * scale2D, worldZ * scale2D, seed + 1) + 1f) * 0.5f;
                        
                        // Humidity noise (different seed)
                        humidityMap[index] = (_humidityNoise.GenSingle2D(worldX * scale2D, worldZ * scale2D, seed + 2) + 1f) * 0.5f;
                    }
                }

                var job = new TerrainGenerationJob
                {
                    ChunkPosition = chunkPos,
                    Blocks = blocks,
                    HeightMap = heightMap,
                    NoiseValues3D = noiseValues3D,
                    HeightOffsets = heightOffsets,
                    TemperatureMap = temperatureMap,
                    HumidityMap = humidityMap,
                    Biomes = BiomesArray,
                    BaseHeight = 64f / Data.ChunkData.HEIGHT,  // Normalize to 0-1 range
                    Strength = 1.0f,
                    WaterLevel = WATER_LEVEL,
                };

                var handle = job.Schedule(Data.ChunkData.SIZE * Data.ChunkData.SIZE, 64);
                handle.Complete();

                // Create a new Data.HeightPoint array and copy the values
                var dataHeightMap = ConvertHeightMap(heightMap);
                var chunkData = new Data.ChunkData
                {
                    position = chunkPos,
                    blocks = blocks,
                    heightMap = dataHeightMap,
                    isEdited = false
                };

                // Use stored settings
                var featureGen = new FeatureGenerator(_settings);
                featureGen.PopulateChunk(ref chunkData, BiomesArray);

                if (callback != null)
                {
                    callback(chunkData);
                }

                // Cleanup
                noiseValues3D.Dispose();
                heightOffsets.Dispose();
                temperatureMap.Dispose();
                humidityMap.Dispose();
                _generatingChunks[int2Pos] = false;

                return handle;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error in GenerateChunkAsync: {e}");
                
                // Cleanup on error
                if (noiseValues3D.IsCreated) noiseValues3D.Dispose();
                if (heightOffsets.IsCreated) heightOffsets.Dispose();
                if (temperatureMap.IsCreated) temperatureMap.Dispose();
                if (humidityMap.IsCreated) humidityMap.Dispose();
                _generatingChunks[int2Pos] = false;
                
                throw;
            }
        }

        private NativeArray<Data.HeightPoint> ConvertHeightMap(NativeArray<Core.HeightPoint> heightMap)
        {
            var dataHeightMap = new NativeArray<Data.HeightPoint>(heightMap.Length, Allocator.Persistent);
            for (int i = 0; i < heightMap.Length; i++)
            {
                dataHeightMap[i] = new Data.HeightPoint
                {
                    height = heightMap[i].height,
                    blockType = heightMap[i].blockType
                };
            }
            return dataHeightMap;
        }

        public void Dispose()
        {
            _generatingChunks.Clear();
            if (BiomesArray.IsCreated)
                BiomesArray.Dispose();
        }
    }
}