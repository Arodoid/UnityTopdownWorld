using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using System;
using System.Collections.Generic;
using WorldSystem.Data;
using WorldSystem.Core;

namespace WorldSystem.Generation
{
    public class WorldGenerator : IDisposable
    {
        private WorldGenSettings settings;
        private NativeArray<BiomeSettings> biomesArray;
        private bool isInitialized;
        private HashSet<int2> generatingChunks = new();
        public int seed { get; private set; }

        public WorldGenerator(WorldGenSettings settings)
        {
            this.settings = settings;
            this.seed = UnityEngine.Random.Range(0, 99999);
            Initialize();
        }

        private void Initialize()
        {
            if (isInitialized) return;
            
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
        
            if (settings.Biomes == null || settings.Biomes.Length == 0)
            {
                settings.Biomes = new BiomeSettings[]
                {
                    CreateDefaultBiome()
                };
            }
        
            biomesArray = new NativeArray<BiomeSettings>(settings.Biomes, Allocator.Persistent);
            isInitialized = true;
        }

        private BiomeSettings CreateDefaultBiome()
        {
            return new BiomeSettings
            {
                BiomeId = 0,
                Temperature = 0.5f,
                Humidity = 0.5f,
                HeightSettings = new BiomeHeightSettings
                {
                    BaseHeight = 64,
                    HeightVariation = 32,
                    TerrainNoiseSettings = new NoiseSettings
                    {
                        Scale = 100,
                        Amplitude = 1,
                        Frequency = 0.01f,
                        Octaves = 4,
                        Persistence = 0.5f,
                        Lacunarity = 2,
                        Seed = seed
                    }
                },
                Layer1 = new BiomeBlockLayer
                {
                    BlockType = BlockType.Dirt,
                    MinDepth = 0,
                    MaxDepth = 4,
                    LayerNoise = new NoiseSettings()
                },
                Layer2 = new BiomeBlockLayer
                {
                    BlockType = BlockType.Stone,
                    MinDepth = 4,
                    MaxDepth = float.MaxValue,
                    LayerNoise = new NoiseSettings()
                },
                LayerCount = 2,
                DefaultSurfaceBlock = BlockType.Grass,
                UnderwaterSurfaceBlock = BlockType.Sand,
                UnderwaterThreshold = 2
            };
        }

        public bool IsGenerating(int2 position) => generatingChunks.Contains(position);

        // Synchronous generation
        public void GenerateChunk(int3 chunkPos, Action<Data.ChunkData> callback)
        {
            var blocks = new NativeArray<byte>(Data.ChunkData.SIZE * Data.ChunkData.SIZE * Data.ChunkData.HEIGHT, 
                Allocator.TempJob);
            var heightMap = new NativeArray<Core.HeightPoint>(Data.ChunkData.SIZE * Data.ChunkData.SIZE, 
                Allocator.TempJob);

            var job = new TerrainGenerationJob
            {
                EnableTerrainHeight = settings.EnableTerrainHeight,
                EnableCaves = settings.Enable3DTerrain,
                EnableWater = settings.EnableWater,
                ChunkPosition = chunkPos,
                Blocks = blocks,
                HeightMap = heightMap,
                Biomes = biomesArray,
                BiomeNoise = settings.BiomeNoiseSettings,
                SeaLevel = settings.SeaLevel,
                DefaultLayerDepth = settings.DefaultLayerDepth,
                DefaultSubsurfaceBlock = (byte)settings.DefaultSubsurfaceBlock,
                DefaultDeepBlock = (byte)settings.DefaultDeepBlock,
                GlobalDensityNoise = settings.GlobalDensityNoise
            };

            job.Schedule(Data.ChunkData.SIZE * Data.ChunkData.SIZE, 64).Complete();

            var chunkHeightMap = new NativeArray<Data.HeightPoint>(heightMap.Length, Allocator.TempJob);
            for (int i = 0; i < heightMap.Length; i++)
            {
                chunkHeightMap[i] = new Data.HeightPoint
                {
                    height = heightMap[i].height,
                    blockType = heightMap[i].blockType
                };
            }

            var chunkData = new Data.ChunkData
            {
                position = chunkPos,
                blocks = blocks,
                heightMap = chunkHeightMap,
                isEdited = false
            };

            heightMap.Dispose();
            callback(chunkData);
        }

        // Asynchronous generation
        public void GenerateChunkAsync(int2 position, Action<Data.ChunkData> callback)
        {
            if (generatingChunks.Contains(position)) return;
            
            generatingChunks.Add(position);
            var chunkPos = new int3(position.x, 0, position.y);
            GenerateChunk(chunkPos, (chunk) => {
                generatingChunks.Remove(position);
                callback(chunk);
            });
        }

        public void Dispose()
        {
            if (biomesArray.IsCreated)
                biomesArray.Dispose();
            isInitialized = false;
        }

        public void UpdateSettings(WorldGenSettings newSettings)
        {
            this.settings = newSettings;
            if (biomesArray.IsCreated)
                biomesArray.Dispose();
            biomesArray = new NativeArray<BiomeSettings>(settings.Biomes, Allocator.Persistent);
        }
    }
} 