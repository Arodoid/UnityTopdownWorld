using Unity.Mathematics;
using Unity.Collections;
using WorldSystem.Core;
using WorldSystem.Data;
using UnityEngine;

namespace WorldSystem.Generation.Features
{
    public class FeatureGenerator
    {
        private readonly WorldSystem.WorldGenSettings _settings;
        private readonly System.Random _random;
        private const float SEA_LEVEL = 120f; // Match WorldGenerator.WATER_LEVEL

        public FeatureGenerator(WorldSystem.WorldGenSettings settings)
        {
            _settings = settings;
            _random = new System.Random(settings.Seed);
        }

        public void PopulateChunk(ref Data.ChunkData chunk, NativeArray<BiomeSettings> biomes)
        {
            
            // Create a feature context to share common generation data
            var context = new FeatureContext
            {
                ChunkPosition = chunk.position,
                Random = new Unity.Mathematics.Random((uint)(_random.Next() + 
                    chunk.position.x * 48611 + 
                    chunk.position.y * 98551 + 
                    chunk.position.z * 13397)),
                Blocks = chunk.blocks,
                HeightMap = chunk.heightMap,
                Biomes = biomes
            };

            // Calculate biome weights for this chunk
            var temperatureMap = new NativeArray<float>(Data.ChunkData.SIZE * Data.ChunkData.SIZE, Allocator.Temp);
            var humidityMap = new NativeArray<float>(Data.ChunkData.SIZE * Data.ChunkData.SIZE, Allocator.Temp);
            var continentalnessMap = new NativeArray<float>(Data.ChunkData.SIZE * Data.ChunkData.SIZE, Allocator.Temp);

            // Fill maps with simple values for now (you might want to use noise here)
            for (int i = 0; i < temperatureMap.Length; i++)
            {
                temperatureMap[i] = 0.5f;
                humidityMap[i] = 0.5f;
                continentalnessMap[i] = 0.5f;
            }

            var biomeGen = new BiomeGenerator(biomes, SEA_LEVEL)
            {
                TemperatureMap = temperatureMap,
                HumidityMap = humidityMap,
                ContinentalnessMap = continentalnessMap
            };

            // Generate features in order of size/importance
            GenerateStructures(ref context);
            GenerateRocks(ref context, biomeGen);
            GenerateTrees(ref context, biomeGen);
            GenerateVegetation(ref context);
            GenerateOres(ref context);

            // Clean up
            temperatureMap.Dispose();
            humidityMap.Dispose();
            continentalnessMap.Dispose();

            // Mark chunk as modified
            chunk.isEdited = true;
        }

        private void GenerateTrees(ref FeatureContext context, BiomeGenerator biomeGen)
        {

            for (int x = 0; x < Data.ChunkData.SIZE; x++)
            {
                for (int z = 0; z < Data.ChunkData.SIZE; z++)
                {
                    int heightMapIndex = x + z * Data.ChunkData.SIZE;
                    int surfaceHeight = context.HeightMap[heightMapIndex].height;
                    byte surfaceBlock = context.HeightMap[heightMapIndex].blockType;
                    
                    // First check: Only allow trees on grass blocks
                    if (surfaceBlock != (byte)BlockType.Grass)
                        continue;

                    // Additional checks
                    if (surfaceHeight <= SEA_LEVEL || 
                        surfaceHeight >= Data.ChunkData.HEIGHT - 10)
                        continue;

                    // Get biome for this position
                    BlockType biomeType = biomeGen.GetSurfaceBlock(x, z, surfaceHeight);
                    BiomeSettings biome = GetBiomeFromSurfaceBlock(context.Biomes, biomeType);

                    // Only generate if biome allows it AND we're on grass
                    if (biome.AllowsTrees && context.Random.NextFloat() < biome.TreeDensity)
                    {
                        int height = (int)math.lerp(biome.TreeMinHeight, biome.TreeMaxHeight, context.Random.NextFloat());
                        GenerateTree(ref context, new int3(x, surfaceHeight + 1, z), height);
                    }
                }
            }
        }

        private BiomeSettings GetBiomeFromSurfaceBlock(NativeArray<BiomeSettings> biomes, BlockType surfaceBlock)
        {
            for (int i = 0; i < biomes.Length; i++)
            {
                if (biomes[i].TopBlock == surfaceBlock)
                    return biomes[i];
            }
            return biomes[1]; // Default to grassland
        }

        private void GenerateTree(ref FeatureContext context, int3 position, int height)
        {
            // Get biome for this position
            int heightMapIndex = position.x + position.z * Data.ChunkData.SIZE;
            byte surfaceBlock = context.HeightMap[heightMapIndex].blockType;
            BiomeSettings biome = GetBiomeFromSurfaceBlock(context.Biomes, (BlockType)surfaceBlock);

            if (biome.IsPalmTree)
            {
                GeneratePalmTree(ref context, position, height);
            }
            else
            {
                GenerateNormalTree(ref context, position, height);
            }
        }

        private void GenerateNormalTree(ref FeatureContext context, int3 position, int height)
        {
            // Generate trunk
            for (int y = 0; y < height - 2; y++)
            {
                SetBlockIfAir(ref context, position + new int3(0, y, 0), (byte)BlockType.Wood);
            }

            // Generate leaves
            int leafStart = height - 3;  // Start leaves 3 blocks from top
            for (int y = 0; y < 3; y++)  // 3 layers of leaves
            {
                int radius = y == 2 ? 1 : 2;  // Top layer is smaller
                
                for (int lx = -radius; lx <= radius; lx++)
                {
                    for (int lz = -radius; lz <= radius; lz++)
                    {
                        // Skip corners for a more natural look
                        if (math.abs(lx) == radius && math.abs(lz) == radius)
                            continue;

                        int3 leafPos = position + new int3(lx, leafStart + y, lz);
                        SetBlockIfAir(ref context, leafPos, (byte)BlockType.Leaves);  // Make sure to use Leaves type
                    }
                }
            }

            // Top leaf
            SetBlockIfAir(ref context, position + new int3(0, height - 1, 0), (byte)BlockType.Leaves);
        }

        private void GeneratePalmTree(ref FeatureContext context, int3 position, int height)
        {
            // Generate slightly curved trunk
            float curve = context.Random.NextFloat() * 0.3f; // Random curve direction
            for (int y = 0; y < height - 2; y++)
            {
                float curveOffset = math.sin(y * 0.2f) * curve;
                int3 trunkPos = position + new int3(
                    (int)(curveOffset * 1.5f),
                    y,
                    (int)(curveOffset * 1.5f)
                );
                SetBlockIfAir(ref context, trunkPos, (byte)BlockType.Wood);
            }

            // Generate palm fronds (leaves)
            int3 topPos = position + new int3(0, height - 2, 0);
            
            // Generate central top leaves
            SetBlockIfAir(ref context, topPos, (byte)BlockType.Leaves);
            SetBlockIfAir(ref context, topPos + new int3(0, 1, 0), (byte)BlockType.Leaves);

            // Generate diagonal fronds in 8 directions
            int[][] directions = new int[][]
            {
                new int[] { 1, 0 }, new int[] { 1, 1 }, new int[] { 0, 1 }, new int[] { -1, 1 },
                new int[] { -1, 0 }, new int[] { -1, -1 }, new int[] { 0, -1 }, new int[] { 1, -1 }
            };

            foreach (var dir in directions)
            {
                if (context.Random.NextFloat() < 0.8f) // 80% chance for each frond
                {
                    int frondLength = (int)(3 + context.Random.NextFloat() * 2);
                    for (int i = 1; i <= frondLength; i++)
                    {
                        int3 frondPos = topPos + new int3(
                            dir[0] * i,
                            math.max(0, 1 - (i / 2)),
                            dir[1] * i
                        );
                        SetBlockIfAir(ref context, frondPos, (byte)BlockType.Leaves);
                    }
                }
            }

            // Add some coconuts (using a different block type, maybe)
            for (int i = 0; i < 4; i++)
            {
                if (context.Random.NextFloat() < 0.3f) // 30% chance for each potential coconut
                {
                    var dir = directions[context.Random.NextInt(0, directions.Length)];
                    int3 coconutPos = topPos + new int3(dir[0], 0, dir[1]);
                    SetBlockIfAir(ref context, coconutPos, (byte)BlockType.Wood); // Using wood for coconuts
                }
            }
        }

        private void GenerateVegetation(ref FeatureContext context)
        {
            // TODO: Implement grass, flowers, etc.
        }

        private void GenerateStructures(ref FeatureContext context)
        {
            // TODO: Implement structures
        }

        private void GenerateOres(ref FeatureContext context)
        {
            // TODO: Implement ore generation
        }

        private void GenerateRocks(ref FeatureContext context, BiomeGenerator biomeGen)
        {
            for (int x = 0; x < Data.ChunkData.SIZE; x++)
            {
                for (int z = 0; z < Data.ChunkData.SIZE; z++)
                {
                    int heightMapIndex = x + z * Data.ChunkData.SIZE;
                    int surfaceHeight = context.HeightMap[heightMapIndex].height;
                    byte surfaceBlock = context.HeightMap[heightMapIndex].blockType;

                    // Skip if underwater or unsuitable surface
                    if (surfaceHeight <= SEA_LEVEL || surfaceBlock == (byte)BlockType.Water)
                        continue;

                    // Get biome for this position
                    BlockType biomeType = biomeGen.GetSurfaceBlock(x, z, surfaceHeight);
                    BiomeSettings biome = GetBiomeFromSurfaceBlock(context.Biomes, biomeType);

                    if (context.Random.NextFloat() < biome.RockDensity)
                    {
                        float rockSize = math.lerp(biome.RockMinSize, biome.RockMaxSize, context.Random.NextFloat());
                        GenerateRock(ref context, new int3(x, surfaceHeight, z), rockSize, biome.RockSpikiness, biome.RockGroundDepth);
                    }
                }
            }
        }

        private void GenerateRock(ref FeatureContext context, int3 position, float size, float spikiness, float groundDepth)
        {
            int radius = (int)math.ceil(size);
            
            // Generate a 3D noise field for the rock shape
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -(int)groundDepth; y <= radius; y++)
                {
                    for (int z = -radius; z <= radius; z++)
                    {
                        float distanceFromCenter = math.sqrt(x * x + y * y + z * z);
                        if (distanceFromCenter > size)
                            continue;

                        // Add some noise to make it less spherical
                        float noise = context.Random.NextFloat() * spikiness;
                        if (distanceFromCenter + noise <= size)
                        {
                            int3 rockPos = position + new int3(x, y, z);
                            // Use gravel occasionally for variety
                            byte blockType = context.Random.NextFloat() < 0.2f ? 
                                (byte)BlockType.Gravel : (byte)BlockType.Stone;
                            SetBlockIfAir(ref context, rockPos, blockType);
                        }
                    }
                }
            }
        }

        private void SetBlockIfAir(ref FeatureContext context, int3 pos, byte blockType)
        {
            // Check bounds
            if (pos.x < 0 || pos.x >= Data.ChunkData.SIZE || 
                pos.y < 0 || pos.y >= Data.ChunkData.HEIGHT || 
                pos.z < 0 || pos.z >= Data.ChunkData.SIZE)
                return;

            int index = pos.x + (pos.z * Data.ChunkData.SIZE) + (pos.y * Data.ChunkData.SIZE * Data.ChunkData.SIZE);
            
            // Only replace air blocks
            if (context.Blocks[index] == (byte)BlockType.Air)
            {
                context.Blocks[index] = blockType;
            }
        }
    }

    public struct FeatureContext
    {
        public int3 ChunkPosition;
        public Unity.Mathematics.Random Random;
        public NativeArray<byte> Blocks;
        public NativeArray<Data.HeightPoint> HeightMap;
        public NativeArray<BiomeSettings> Biomes;
    }
} 