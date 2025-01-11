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
                Continentalness = 0.5f,
                DensitySettings = new TerrainDensitySettings
                {
                    DeepStart = 0,
                    CaveStart = 40,
                    CaveEnd = 60,
                    SurfaceStart = 80,
                    SurfaceEnd = 100,
                    DeepBias = 1f,
                    CaveBias = 0f,
                    SurfaceBias = 0.5f,
                    DeepTransitionScale = 0.1f,
                    CaveTransitionScale = 1f,
                    AirTransitionScale = 0.1f,
                    DeepTransitionCurve = 2f,
                    CaveTransitionCurve = 0.5f,
                    AirTransitionCurve = 1.5f
                },
                PrimaryBlock = BlockType.Dirt,
                SecondaryBlock = BlockType.Stone,
                TopBlock = BlockType.Grass,
                UnderwaterBlock = BlockType.Sand,
                UnderwaterThreshold = 2f
            };
        }

        public bool IsGenerating(int2 position) => generatingChunks.Contains(position);

        // Synchronous generation
        public void GenerateChunk(int3 chunkPos, Action<Data.ChunkData> callback)
        {
            // Create native arrays with TempJob allocator
            var blocks = new NativeArray<byte>(Data.ChunkData.SIZE * Data.ChunkData.SIZE * Data.ChunkData.HEIGHT, Allocator.TempJob);
            var heightMap = new NativeArray<Core.HeightPoint>(Data.ChunkData.SIZE * Data.ChunkData.SIZE, Allocator.TempJob);
            var finalHeightMap = new NativeArray<Data.HeightPoint>(Data.ChunkData.SIZE * Data.ChunkData.SIZE, Allocator.Persistent);

            try
            {
                var job = new TerrainGenerationJob
                {
                    ChunkPosition = chunkPos,
                    Blocks = blocks,
                    HeightMap = heightMap,
                    Biomes = biomesArray,
                    BiomeNoise = settings.BiomeNoiseSettings,
                    BiomeFalloff = settings.BiomeFalloff,
                    SeaLevel = settings.SeaLevel,
                    EnableCaves = settings.Enable3DTerrain,
                    EnableWater = settings.EnableWater,
                    GlobalDensityNoise = settings.GlobalDensityNoise,
                    OceanThreshold = settings.OceanThreshold,
                };

                job.Schedule(Data.ChunkData.SIZE * Data.ChunkData.SIZE, 64).Complete();

                // Convert heightMap to final format
                for (int i = 0; i < heightMap.Length; i++)
                {
                    finalHeightMap[i] = new Data.HeightPoint
                    {
                        height = heightMap[i].height,
                        blockType = heightMap[i].blockType
                    };
                }

                // Create the final chunk data
                var chunkData = new Data.ChunkData
                {
                    position = chunkPos,
                    blocks = new NativeArray<byte>(blocks, Allocator.Persistent),
                    heightMap = finalHeightMap,
                    isEdited = false
                };

                callback(chunkData);
            }
            finally
            {
                // Ensure we always dispose of temporary arrays
                if (blocks.IsCreated) blocks.Dispose();
                if (heightMap.IsCreated) heightMap.Dispose();
                // Note: finalHeightMap is transferred to ChunkData ownership
            }
        }

        // Asynchronous generation
        public JobHandle GenerateChunkAsync(int3 chunkPos, NativeArray<byte> blocks, 
            NativeArray<Core.HeightPoint> heightMap, Action<Data.ChunkData> callback)
        {
            var job = new TerrainGenerationJob
            {
                ChunkPosition = chunkPos,
                Blocks = blocks,
                HeightMap = heightMap,
                Biomes = biomesArray,
                BiomeNoise = settings.BiomeNoiseSettings,
                BiomeFalloff = settings.BiomeFalloff,
                SeaLevel = settings.SeaLevel,
                EnableCaves = settings.Enable3DTerrain,
                EnableWater = settings.EnableWater,
                GlobalDensityNoise = settings.GlobalDensityNoise,
                OceanThreshold = settings.OceanThreshold,
            };

            // Return the JobHandle instead of completing immediately
            return job.Schedule(Data.ChunkData.SIZE * Data.ChunkData.SIZE, 64);
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