using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
using WorldSystem.Core;
using WorldSystem.Data;

namespace WorldSystem.Generation
{
    [BurstCompile]
    public struct TerrainGenerationJob : IJobParallelFor
    {
        [ReadOnly] public int3 ChunkPosition;
        [ReadOnly] public NativeArray<BiomeSettings> Biomes;
        [ReadOnly] public NoiseSettings BiomeNoise;
        [ReadOnly] public float SeaLevel;
        [ReadOnly] public byte DefaultDeepBlock;
        [ReadOnly] public bool EnableCaves;
        [ReadOnly] public bool EnableWater;
        
        [NativeDisableParallelForRestriction]
        public NativeArray<byte> Blocks;
        [NativeDisableParallelForRestriction]
        public NativeArray<Core.HeightPoint> HeightMap;
        [ReadOnly] public NoiseSettings GlobalDensityNoise;
        [ReadOnly] public float OceanThreshold;
        [ReadOnly] public float SeaCaveDepth;

        public void Execute(int index)
        {
            // Calculate x,z coordinates from the index
            int x = index % Core.ChunkData.SIZE;
            int z = index / Core.ChunkData.SIZE;
            
            float2 worldPos = new float2(
                ChunkPosition.x * Core.ChunkData.SIZE + x,
                ChunkPosition.z * Core.ChunkData.SIZE + z
            );

            // Get biome weights
            using var biomeWeights = new NativeArray<float>(Biomes.Length, Allocator.Temp);
            var biomeGen = new BiomeGenerator(BiomeNoise, Biomes, 4);
            biomeGen.GetBiomeWeights(worldPos, biomeWeights);

            // Generate blocks for this column
            for (int y = 0; y < Core.ChunkData.HEIGHT; y++)
            {
                int blockIndex = GetBlockIndex(x, y, z);
                if (blockIndex >= 0 && blockIndex < Blocks.Length)
                {
                    Blocks[blockIndex] = DetermineBlockType(x, y, z, biomeWeights);
                }
            }

            // Store surface information for later use
            StoreSurfaceInfo(x, z, biomeWeights);
        }

        private void StoreSurfaceInfo(int x, int z, NativeArray<float> biomeWeights)
        {
            int heightMapIndex = x + z * Core.ChunkData.SIZE;
            
            // Blend surface parameters
            float underwaterThreshold = 0f;
            BlockType topBlock = BlockType.Air;
            BlockType underwaterBlock = BlockType.Air;
            float totalWeight = 0f;
            
            for (int i = 0; i < Biomes.Length; i++)
            {
                float weight = biomeWeights[i];
                if (weight > 0.01f)
                {
                    underwaterThreshold += Biomes[i].UnderwaterThreshold * weight;
                    topBlock = GetWeightedBlock(topBlock, Biomes[i].TopBlock, weight);
                    underwaterBlock = GetWeightedBlock(underwaterBlock, Biomes[i].UnderwaterBlock, weight);
                    totalWeight += weight;
                }
            }
            
            if (totalWeight > 0)
            {
                underwaterThreshold /= totalWeight;
            }
            
            // Find surface height
            int surfaceHeight = 0;
            for (int y = Core.ChunkData.HEIGHT - 1; y >= 0; y--)
            {
                int blockIndex = GetBlockIndex(x, y, z);
                if (Blocks[blockIndex] != (byte)BlockType.Air)
                {
                    surfaceHeight = y;
                    break;
                }
            }

            bool isUnderwater = surfaceHeight < SeaLevel + underwaterThreshold;
            byte surfaceBlock = (byte)(isUnderwater ? underwaterBlock : topBlock);

            HeightMap[heightMapIndex] = new Core.HeightPoint
            {
                height = (byte)math.clamp(surfaceHeight, 0, 255),
                blockType = surfaceBlock
            };
        }

        private int GetBlockIndex(int x, int y, int z)
        {
            return x + (z * Core.ChunkData.SIZE) + (y * Core.ChunkData.SIZE * Core.ChunkData.SIZE);
        }

        private byte DetermineBlockType(int x, int y, int z, NativeArray<float> biomeWeights)
        {
            float2 worldPos = new float2(
                ChunkPosition.x * Core.ChunkData.SIZE + x,
                ChunkPosition.z * Core.ChunkData.SIZE + z
            );
            float3 worldPos3D = new float3(worldPos.x, y, worldPos.y);

            // Blend ALL biome parameters
            float density = 0f;
            float layerDepth = 0f;
            float underwaterThreshold = 0f;
            BlockType primaryBlock = BlockType.Air;
            BlockType secondaryBlock = BlockType.Air;
            BlockType topBlock = BlockType.Air;
            BlockType underwaterBlock = BlockType.Air;
            float totalWeight = 0f;

            // First blend density settings
            TerrainDensitySettings blendedDensitySettings = new TerrainDensitySettings();
            
            for (int i = 0; i < Biomes.Length; i++)
            {
                float weight = biomeWeights[i];
                if (weight > 0.01f)
                {
                    var biome = Biomes[i];
                    var settings = biome.DensitySettings;
                    
                    // Blend density settings
                    blendedDensitySettings.DeepStart += settings.DeepStart * weight;
                    blendedDensitySettings.CaveStart += settings.CaveStart * weight;
                    blendedDensitySettings.CaveEnd += settings.CaveEnd * weight;
                    blendedDensitySettings.SurfaceStart += settings.SurfaceStart * weight;
                    blendedDensitySettings.SurfaceEnd += settings.SurfaceEnd * weight;
                    blendedDensitySettings.DeepBias += settings.DeepBias * weight;
                    blendedDensitySettings.CaveBias += settings.CaveBias * weight;
                    blendedDensitySettings.SurfaceBias += settings.SurfaceBias * weight;
                    blendedDensitySettings.DeepTransitionScale += settings.DeepTransitionScale * weight;
                    blendedDensitySettings.CaveTransitionScale += settings.CaveTransitionScale * weight;
                    blendedDensitySettings.AirTransitionScale += settings.AirTransitionScale * weight;
                    blendedDensitySettings.DeepTransitionCurve += settings.DeepTransitionCurve * weight;
                    blendedDensitySettings.CaveTransitionCurve += settings.CaveTransitionCurve * weight;
                    blendedDensitySettings.AirTransitionCurve += settings.AirTransitionCurve * weight;
                    
                    // Blend other parameters
                    underwaterThreshold += biome.UnderwaterThreshold * weight;
                    
                    // For block types, use weighted voting
                    primaryBlock = GetWeightedBlock(primaryBlock, biome.PrimaryBlock, weight);
                    secondaryBlock = GetWeightedBlock(secondaryBlock, biome.SecondaryBlock, weight);
                    topBlock = GetWeightedBlock(topBlock, biome.TopBlock, weight);
                    underwaterBlock = GetWeightedBlock(underwaterBlock, biome.UnderwaterBlock, weight);
                    
                    totalWeight += weight;
                }
            }

            if (totalWeight > 0)
            {
                // Get base 3D noise value using global settings (or blend noise settings if biomes have different noise)
                float noise = NoiseUtility.Sample3D(worldPos3D, GlobalDensityNoise);
                
                // Calculate density using blended settings
                density = CalculateDensity(noise, y, blendedDensitySettings);

                if (density > 0f)
                {
                    int heightMapIndex = x + z * Core.ChunkData.SIZE;
                    float surfaceHeight = HeightMap[heightMapIndex].height;
                    float depthFromSurface = surfaceHeight - y;

                    // Use blended parameters for block selection
                    if (depthFromSurface > layerDepth)
                    {
                        return (byte)secondaryBlock;
                    }
                    else
                    {
                        return (byte)primaryBlock;
                    }
                }
                else if (EnableWater && y <= SeaLevel)
                {
                    return (byte)BlockType.Water;
                }
            }
            
            return (byte)BlockType.Air;
        }

        private BlockType GetWeightedBlock(BlockType currentBlock, BlockType newBlock, float weight)
        {
            // Simple weighted voting system for block types
            if (currentBlock == BlockType.Air || weight > 0.5f)
            {
                return newBlock;
            }
            return currentBlock;
        }

        private float CalculateDensity(float baseNoise, float y, TerrainDensitySettings settings)
        {
            float density = baseNoise;

            // Deep Zone
            if (y < settings.CaveStart)
            {
                float t = (settings.CaveStart - y) / (settings.CaveStart - settings.DeepStart);
                float deepBias = math.pow(t, settings.DeepTransitionCurve) * settings.DeepTransitionScale;
                density += deepBias;
            }
            // Cave Zone
            else if (y < settings.CaveEnd)
            {
                density += settings.CaveBias;
            }
            // Cave-to-Surface Transition
            else if (y < settings.SurfaceStart)
            {
                float t = (y - settings.CaveEnd) / (settings.SurfaceStart - settings.CaveEnd);
                float curvedT = math.pow(t, settings.CaveTransitionCurve);
                density += settings.CaveBias + (settings.SurfaceBias - settings.CaveBias) * curvedT;
            }
            // Surface Zone - CONSTANT BIAS
            else if (y < settings.SurfaceEnd)
            {
                density += settings.SurfaceBias;
            }
            // Air Transition - Now starts explicitly from SurfaceBias
            else
            {
                float t = (y - settings.SurfaceEnd) / (settings.SurfaceEnd - settings.SurfaceStart);
                float airBias = math.pow(t, settings.AirTransitionCurve) * settings.AirTransitionScale;
                density = baseNoise + settings.SurfaceBias - airBias;  // Explicitly start from SurfaceBias
            }

            return density;
        }

        private bool ShouldGenerateOcean(float2 worldPos)
        {
            float continentalness = NoiseUtility.Sample2D(worldPos + 2000f, 
                new NoiseSettings 
                { 
                    Scale = BiomeNoise.Scale * 3,
                    Amplitude = BiomeNoise.Amplitude,
                    Frequency = BiomeNoise.Frequency * 0.3f,
                    Octaves = BiomeNoise.Octaves,
                    Persistence = BiomeNoise.Persistence,
                    Lacunarity = BiomeNoise.Lacunarity,
                    Seed = BiomeNoise.Seed + 1000
                });

            return continentalness < OceanThreshold;
        }
    }
}