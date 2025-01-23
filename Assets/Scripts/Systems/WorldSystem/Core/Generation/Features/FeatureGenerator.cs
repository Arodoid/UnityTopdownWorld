using Unity.Mathematics;
using Unity.Collections;
using WorldSystem.Core;
using WorldSystem.Data;
using UnityEngine;

namespace WorldSystem.Generation.Features
{
    public class FeatureGenerator
    {
        private readonly WorldGenSettings _settings;
        private readonly System.Random _random;
        private const float SEA_LEVEL = 64f; // Made this a constant since it's not in settings

        public FeatureGenerator(WorldGenSettings settings)
        {
            UnityEngine.Debug.Log($"Creating FeatureGenerator with seed {settings.Seed}");
            _settings = settings;
            _random = new System.Random(settings.Seed);
        }

        public void PopulateChunk(ref Data.ChunkData chunk, NativeArray<BiomeSettings> biomes)
        {
            UnityEngine.Debug.Log($"Starting feature generation for chunk at {chunk.position}");
            
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

            UnityEngine.Debug.Log($"Context created. Biomes count: {biomes.Length}");

            // Generate features in order of size/importance
            GenerateStructures(ref context);
            GenerateRocks(ref context);
            GenerateTrees(ref context);
            GenerateVegetation(ref context);
            GenerateOres(ref context);

            // Mark chunk as modified
            chunk.isEdited = true;

            UnityEngine.Debug.Log($"Feature generation complete for chunk at {chunk.position}");
        }

        private void GenerateTrees(ref FeatureContext context)
        {
            UnityEngine.Debug.Log($"Starting tree generation for chunk at {context.ChunkPosition}");

            // Get biome weights for this chunk's position
            var biomeWeights = new NativeArray<float>(context.Biomes.Length, Allocator.Temp);
            var biomeGen = new BiomeGenerator(new NoiseSettings { 
                Scale = 3f, 
                Amplitude = 1.3f, 
                Frequency = 0.01f, 
                Octaves = 4, 
                Persistence = 0.5f, 
                Lacunarity = 2f,
                Seed = _settings.Seed
            }, context.Biomes, 5f);

            float2 chunkWorldPos = new float2(
                context.ChunkPosition.x * Data.ChunkData.SIZE,
                context.ChunkPosition.z * Data.ChunkData.SIZE
            );

            biomeGen.GetBiomeWeights(chunkWorldPos, biomeWeights);

            // Calculate blended tree density for this position
            float treeDensity = 0f;
            bool allowTrees = false;
            float minHeight = 4f;
            float maxHeight = 8f;

            UnityEngine.Debug.Log($"Calculating tree parameters for chunk. Biome weights: {string.Join(", ", biomeWeights.ToArray())}");

            for (int i = 0; i < context.Biomes.Length; i++)
            {
                float weight = biomeWeights[i];
                if (weight > 0.01f)
                {
                    treeDensity += context.Biomes[i].TreeDensity * weight;
                    if (context.Biomes[i].AllowsTrees)
                        allowTrees = true;
                    minHeight += context.Biomes[i].TreeMinHeight * weight;
                    maxHeight += context.Biomes[i].TreeMaxHeight * weight;

                    UnityEngine.Debug.Log($"Biome {i}: Weight={weight}, TreeDensity={context.Biomes[i].TreeDensity}, AllowsTrees={context.Biomes[i].AllowsTrees}");
                }
            }

            biomeWeights.Dispose();

            UnityEngine.Debug.Log($"Final tree parameters: Density={treeDensity}, AllowTrees={allowTrees}, MinHeight={minHeight}, MaxHeight={maxHeight}");

            if (!allowTrees || treeDensity <= 0f)
            {
                UnityEngine.Debug.Log("No trees will be generated in this chunk.");
                return;
            }

            int treesAttempted = 0;
            int treesPlaced = 0;

            // Generate trees based on density
            for (int x = 0; x < Data.ChunkData.SIZE; x++)
            {
                for (int z = 0; z < Data.ChunkData.SIZE; z++)
                {
                    int heightMapIndex = x + z * Data.ChunkData.SIZE;
                    int surfaceHeight = context.HeightMap[heightMapIndex].height;
                    byte surfaceBlock = context.HeightMap[heightMapIndex].blockType;
                    
                    // Skip if underwater or too high
                    if (surfaceHeight <= SEA_LEVEL || surfaceHeight >= Data.ChunkData.HEIGHT - 10)
                        continue;

                    // Skip if surface block isn't suitable
                    if (surfaceBlock == (byte)BlockType.Sand || surfaceBlock == (byte)BlockType.Water)
                        continue;

                    treesAttempted++;
                    // Check if we should place a tree here
                    if (context.Random.NextFloat() < treeDensity)
                    {
                        int height = (int)math.lerp(minHeight, maxHeight, context.Random.NextFloat());
                        GenerateTree(ref context, new int3(x, surfaceHeight + 1, z), height);
                        treesPlaced++;
                    }
                }
            }

            UnityEngine.Debug.Log($"Tree generation complete. Attempted={treesAttempted}, Placed={treesPlaced}");
        }

        private void GenerateTree(ref FeatureContext context, int3 position, int height)
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

        private void GenerateRocks(ref FeatureContext context)
        {
            UnityEngine.Debug.Log($"Starting rock generation for chunk at {context.ChunkPosition}");

            // Get biome weights like we do for trees
            var biomeWeights = new NativeArray<float>(context.Biomes.Length, Allocator.Temp);
            var biomeGen = new BiomeGenerator(new NoiseSettings { 
                Scale = 3f, 
                Amplitude = 1.3f, 
                Frequency = 0.01f, 
                Octaves = 4, 
                Persistence = 0.5f, 
                Lacunarity = 2f,
                Seed = _settings.Seed + 500 // Different seed than trees
            }, context.Biomes, 5f);

            float2 chunkWorldPos = new float2(
                context.ChunkPosition.x * Data.ChunkData.SIZE,
                context.ChunkPosition.z * Data.ChunkData.SIZE
            );

            biomeGen.GetBiomeWeights(chunkWorldPos, biomeWeights);

            // Calculate blended rock parameters
            float rockDensity = 0f;
            bool allowRocks = false;
            float minSize = 2f;
            float maxSize = 4f;
            float spikiness = 0.5f;
            float groundDepth = 1f;

            for (int i = 0; i < context.Biomes.Length; i++)
            {
                float weight = biomeWeights[i];
                if (weight > 0.01f)
                {
                    rockDensity += context.Biomes[i].RockDensity * weight;
                    if (context.Biomes[i].AllowsRocks)
                        allowRocks = true;
                    minSize += context.Biomes[i].RockMinSize * weight;
                    maxSize += context.Biomes[i].RockMaxSize * weight;
                    spikiness += context.Biomes[i].RockSpikiness * weight;
                    groundDepth += context.Biomes[i].RockGroundDepth * weight;
                }
            }

            biomeWeights.Dispose();

            if (!allowRocks || rockDensity <= 0f)
                return;

            // Generate rocks
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

                    if (context.Random.NextFloat() < rockDensity)
                    {
                        float size = math.lerp(minSize, maxSize, context.Random.NextFloat());
                        GenerateRock(ref context, new int3(x, surfaceHeight, z), size, spikiness, groundDepth);
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