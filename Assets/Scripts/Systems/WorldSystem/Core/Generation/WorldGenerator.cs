using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using System;
using System.Collections.Generic;
using WorldSystem.Data;
using WorldSystem.Core;
using WorldSystem.Generation.Features;

namespace WorldSystem.Generation
{
    public class WorldGenerator : IDisposable
    {
        private readonly WorldGenSettings _settings;
        private NativeArray<BiomeSettings> _biomesArray;
        private bool _isInitialized;
        private HashSet<int2> _generatingChunks = new();
        
        // Hardcoded world generation settings
        private readonly BiomeSettings[] DEFAULT_BIOMES;
        private readonly NoiseSettings BIOME_NOISE = new() { Scale = 3f, Amplitude = 1.3f, Frequency = 0.01f, Octaves = 4, Persistence = 0.5f, Lacunarity = 2f };
        private const float BIOME_FALLOFF = 5f;
        private const float SEA_LEVEL = 64f;
        private const bool ENABLE_3D_TERRAIN = true;
        private const bool ENABLE_WATER = true;
        private const float OCEAN_THRESHOLD = 0.5f;
        private readonly NoiseSettings GLOBAL_DENSITY_NOISE = new() { Scale = 2f, Amplitude = 2f, Frequency = 0.01f, Octaves = 4, Persistence = 0.5f, Lacunarity = 2f };

        // Add this property to access biomes array
        public NativeArray<BiomeSettings> BiomesArray => _biomesArray;

        public int seed => _settings.Seed;

        public WorldGenerator(WorldGenSettings settings)
        {
            _settings = settings;
            BIOME_NOISE.Seed = settings.Seed;
            GLOBAL_DENSITY_NOISE.Seed = settings.Seed;
            DEFAULT_BIOMES = CreateDefaultBiomes();
            Initialize();
        }

        private void Initialize()
        {
            if (_isInitialized) return;
            _biomesArray = new NativeArray<BiomeSettings>(DEFAULT_BIOMES, Allocator.Persistent);
            _isInitialized = true;
        }

        private BiomeSettings[] CreateDefaultBiomes()
        {
            return new BiomeSettings[]
            {
                // Ocean biome (lowest elevation, lowest continentalness)
                new BiomeSettings
                {
                    BiomeId = 0,
                    Temperature = 0.5f,
                    Humidity = 0.5f,
                    Continentalness = 0.50f,
                    DensitySettings = new TerrainDensitySettings
                    {
                        DeepStart = 0,
                        CaveStart = 30,
                        CaveEnd = 45,
                        SurfaceStart = 30,
                        SurfaceEnd = 40,
                        DeepBias = 1f,
                        CaveBias = 0f,
                        SurfaceBias = 0.8f,
                        DeepTransitionScale = 1f,
                        CaveTransitionScale = 1f,
                        AirTransitionScale = 1f,
                        DeepTransitionCurve = 1f,
                        CaveTransitionCurve = 1f,
                        AirTransitionCurve = 1f
                    },
                    PrimaryBlock = BlockType.Sand,
                    SecondaryBlock = BlockType.Stone,
                    TopBlock = BlockType.Sand,
                    UnderwaterBlock = BlockType.Sand,
                    UnderwaterThreshold = 1f,
                    TreeDensity = 0f,
                    AllowsTrees = false,
                    RockDensity = 0f,
                    AllowsRocks = false,
                },

                // Beach biome
                new BiomeSettings
                {
                    BiomeId = 1,
                    Temperature = 0.5f,
                    Humidity = 0.5f,
                    Continentalness = 0.60f,
                    DensitySettings = new TerrainDensitySettings
                    {
                        DeepStart = 0,
                        CaveStart = 40,
                        CaveEnd = 55,
                        SurfaceStart = 55,
                        SurfaceEnd = 64,
                        DeepBias = 1f,
                        CaveBias = 0f,
                        SurfaceBias = 0.4f,
                        DeepTransitionScale = 1f,
                        CaveTransitionScale = 1f,
                        AirTransitionScale = 1f,
                        DeepTransitionCurve = 1f,
                        CaveTransitionCurve = 1f,
                        AirTransitionCurve = 1f
                    },
                    PrimaryBlock = BlockType.Sand,
                    SecondaryBlock = BlockType.Stone,
                    TopBlock = BlockType.Sand,
                    UnderwaterBlock = BlockType.Sand,
                    UnderwaterThreshold = 2f,
                    TreeDensity = 0.001f,  // Very rare trees
                    TreeMinHeight = 4,
                    TreeMaxHeight = 6,
                    AllowsTrees = true,
                    RockDensity = 0.00002f,
                    RockMinSize = 1f,
                    RockMaxSize = 2f,
                    RockSpikiness = 0.3f,
                    RockGroundDepth = 0.5f,
                    AllowsRocks = true,
                },

                // Grassland biome
                new BiomeSettings
                {
                    BiomeId = 2,
                    Temperature = 0.5f,
                    Humidity = 0.5f,
                    Continentalness = 0.65f,
                    DensitySettings = new TerrainDensitySettings
                    {
                        DeepStart = 0,
                        CaveStart = 50,
                        CaveEnd = 70,
                        SurfaceStart = 64,
                        SurfaceEnd = 70,
                        DeepBias = 1f,
                        CaveBias = 0f,
                        SurfaceBias = 0.5f,
                        DeepTransitionScale = 1f,
                        CaveTransitionScale = 1f,
                        AirTransitionScale = 1f,
                        DeepTransitionCurve = 1f,
                        CaveTransitionCurve = 1f,
                        AirTransitionCurve = 1f
                    },
                    PrimaryBlock = BlockType.Grass,
                    SecondaryBlock = BlockType.Stone,
                    TopBlock = BlockType.Grass,
                    UnderwaterBlock = BlockType.Grass,
                    UnderwaterThreshold = 2f,
                    TreeDensity = 0.02f,   // More common trees
                    TreeMinHeight = 4,
                    TreeMaxHeight = 8,
                    AllowsTrees = true,
                    RockDensity = 0.0003f,
                    RockMinSize = 0.5f,
                    RockMaxSize = 1f,
                    RockSpikiness = 0.4f,
                    RockGroundDepth = 1f,
                    AllowsRocks = true,
                },

                // Mountain biome (highest elevation, highest continentalness)
                new BiomeSettings
                {
                    BiomeId = 3,
                    Temperature = 0.5f,
                    Humidity = 0.5f,
                    Continentalness = 0.95f,
                    DensitySettings = new TerrainDensitySettings
                    {
                        DeepStart = 0,
                        CaveStart = 70,
                        CaveEnd = 100,
                        SurfaceStart = 70,
                        SurfaceEnd = 110,
                        DeepBias = 1f,
                        CaveBias = 0.2f,
                        SurfaceBias = 0.4f,
                        DeepTransitionScale = 1f,
                        CaveTransitionScale = 1f,
                        AirTransitionScale = 0.7f,
                        DeepTransitionCurve = 1f,
                        CaveTransitionCurve = 1f,
                        AirTransitionCurve = 1f
                    },
                    PrimaryBlock = BlockType.Stone,
                    SecondaryBlock = BlockType.Stone,
                    TopBlock = BlockType.Stone,
                    UnderwaterBlock = BlockType.Gravel,
                    UnderwaterThreshold = 3f,
                    TreeDensity = 0.01f,   // Moderate tree density
                    TreeMinHeight = 3,
                    TreeMaxHeight = 6,
                    AllowsTrees = true,
                    RockDensity = 0.0004f,
                    RockMinSize = 1f,
                    RockMaxSize = 2f,
                    RockSpikiness = 0.7f,
                    RockGroundDepth = 1.5f,
                    AllowsRocks = true,
                }
            };
        }

        public bool IsGenerating(int2 position) => _generatingChunks.Contains(position);

        // Synchronous generation
        public void GenerateChunk(int3 chunkPos, Action<Data.ChunkData> callback)
        {
            UnityEngine.Debug.Log($"Starting chunk generation at {chunkPos}");
            
            // Allocate with Persistent instead of TempJob
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
                    Biomes = _biomesArray,
                    BiomeNoise = BIOME_NOISE,
                    BiomeFalloff = BIOME_FALLOFF,
                    SeaLevel = SEA_LEVEL,
                    EnableCaves = ENABLE_3D_TERRAIN,
                    EnableWater = ENABLE_WATER,
                    GlobalDensityNoise = GLOBAL_DENSITY_NOISE,
                    OceanThreshold = OCEAN_THRESHOLD,
                };

                // Complete the job immediately since we're not async here
                job.Schedule(Data.ChunkData.SIZE * Data.ChunkData.SIZE, 64).Complete();

                // Convert heightMap to final format
                var finalHeightMap = new NativeArray<Data.HeightPoint>(Data.ChunkData.SIZE * Data.ChunkData.SIZE, Allocator.Persistent);
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
                    blocks = blocks,
                    heightMap = finalHeightMap,
                    isEdited = false
                };

                // After terrain generation is complete
                UnityEngine.Debug.Log("Terrain generation complete, starting feature generation");
                var featureGenerator = new FeatureGenerator(_settings);
                featureGenerator.PopulateChunk(ref chunkData, _biomesArray);
                UnityEngine.Debug.Log("Feature generation complete");

                callback(chunkData);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"Error generating chunk: {e}");
                // Ensure cleanup on error
                if (blocks.IsCreated) blocks.Dispose();
                if (heightMap.IsCreated) heightMap.Dispose();
                throw;
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
                Biomes = _biomesArray,
                BiomeNoise = BIOME_NOISE,
                BiomeFalloff = BIOME_FALLOFF,
                SeaLevel = SEA_LEVEL,
                EnableCaves = ENABLE_3D_TERRAIN,
                EnableWater = ENABLE_WATER,
                GlobalDensityNoise = GLOBAL_DENSITY_NOISE,
                OceanThreshold = OCEAN_THRESHOLD,
            };

            return job.Schedule(Data.ChunkData.SIZE * Data.ChunkData.SIZE, 64);
        }

        public void Dispose()
        {
            if (_biomesArray.IsCreated)
                _biomesArray.Dispose();
            _isInitialized = false;
        }
    }
} 