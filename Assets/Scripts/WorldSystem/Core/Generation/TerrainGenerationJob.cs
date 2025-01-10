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
        [ReadOnly] public float DefaultLayerDepth;
        [ReadOnly] public byte DefaultDeepBlock;
        [ReadOnly] public bool EnableTerrainHeight;
        [ReadOnly] public bool EnableCaves;
        [ReadOnly] public bool EnableWater;
        
        [NativeDisableParallelForRestriction]
        public NativeArray<byte> Blocks;
        [NativeDisableParallelForRestriction]
        public NativeArray<Core.HeightPoint> HeightMap;
        [ReadOnly] public NoiseSettings GlobalDensityNoise;

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
            var dominantBiome = GetDominantBiome(biomeWeights);
            
            // Find the highest solid block in this column
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

            HeightMap[heightMapIndex] = new Core.HeightPoint
            {
                height = (byte)math.clamp(surfaceHeight, 0, 255),
                blockType = DetermineSurfaceBlock(surfaceHeight, dominantBiome)
            };
        }

        private int GetBlockIndex(int x, int y, int z)
        {
            return x + (z * Core.ChunkData.SIZE) + (y * Core.ChunkData.SIZE * Core.ChunkData.SIZE);
        }

        private byte DetermineBlockType(int x, int y, int z, NativeArray<float> biomeWeights)
        {
            float3 worldPos = new float3(
                ChunkPosition.x * Core.ChunkData.SIZE + x,
                y,
                ChunkPosition.z * Core.ChunkData.SIZE + z
            );

            // Get base 3D noise value (-1 to 1)
            float noise = NoiseUtility.Sample3D(worldPos, GlobalDensityNoise) * 2f - 1f;
            
            float density = 0f;
            float totalWeight = 0f;
            
            // Blend density settings from different biomes
            for (int i = 0; i < Biomes.Length; i++)
            {
                float weight = biomeWeights[i];
                if (weight > 0.01f)
                {
                    var densitySettings = Biomes[i].DensitySettings;
                    float localDensity = CalculateDensity(noise, y, densitySettings);
                    density += localDensity * weight;
                    totalWeight += weight;
                }
            }
            
            if (totalWeight > 0)
            {
                density /= totalWeight;
                if (density > 0f)
                {
                    var dominantBiome = GetDominantBiome(biomeWeights);
                    int heightMapIndex = x + z * Core.ChunkData.SIZE;
                    float surfaceHeight = HeightMap[heightMapIndex].height;
                    float depthFromSurface = surfaceHeight - y;

                    // Determine block type based on depth
                    if (depthFromSurface > DefaultLayerDepth)
                    {
                        return (byte)dominantBiome.SecondaryBlock;
                    }
                    else
                    {
                        return (byte)dominantBiome.PrimaryBlock;
                    }
                }
            }
            
            return (byte)BlockType.Air;
        }

        private bool CheckLayer(BiomeBlockLayer layer, float depth, int worldY, int heightInt, out byte blockType)
        {
            blockType = 0;
            if (depth >= layer.MinDepth && depth <= layer.MaxDepth)
            {
                if (layer.LayerNoise.Scale > 0)
                {
                    float noise = NoiseUtility.Sample3D(
                        new float3(worldY, heightInt, depth), 
                        layer.LayerNoise
                    );
                    if (noise > 0)
                    {
                        blockType = (byte)layer.BlockType;
                        return true;
                    }
                }
                else
                {
                    blockType = (byte)layer.BlockType;
                    return true;
                }
            }
            return false;
        }

        private byte DetermineSurfaceBlock(float height, BiomeSettings biome)
        {
            bool isUnderwater = height < SeaLevel + biome.UnderwaterThreshold;
            return (byte)(isUnderwater ? biome.UnderwaterBlock : biome.TopBlock);
        }

        private BiomeSettings GetDominantBiome(NativeArray<float> weights)
        {
            int dominantIndex = 0;
            float maxWeight = weights[0];
            
            for (int i = 1; i < weights.Length; i++)
            {
                if (weights[i] > maxWeight)
                {
                    maxWeight = weights[i];
                    dominantIndex = i;
                }
            }
            
            return Biomes[dominantIndex];
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
            // Surface Zone
            else if (y < settings.SurfaceEnd)
            {
                density += settings.SurfaceBias;
            }
            // Air Zone
            else
            {
                float t = (y - settings.SurfaceEnd) / (settings.SurfaceEnd - settings.SurfaceStart);
                float airBias = math.pow(t, settings.AirTransitionCurve) * settings.AirTransitionScale;
                density -= airBias;
            }

            return density;
        }
    }
}