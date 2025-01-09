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
        [ReadOnly] public byte DefaultSubsurfaceBlock;
        [ReadOnly] public byte DefaultDeepBlock;
        [ReadOnly] public bool EnableTerrainHeight;
        [ReadOnly] public bool EnableCaves;
        [ReadOnly] public bool EnableWater;
        
        [NativeDisableParallelForRestriction]
        public NativeArray<byte> Blocks;
        [WriteOnly] public NativeArray<Core.HeightPoint> HeightMap;
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

            // Create temporary array for biome weights
            using var biomeWeights = new NativeArray<float>(Biomes.Length, Allocator.Temp);
            var biomeGen = new BiomeGenerator(BiomeNoise, Biomes, 4);
            biomeGen.GetBiomeWeights(worldPos, biomeWeights);

            var dominantBiome = GetDominantBiome(biomeWeights);
            float terrainHeight = CalculateTerrainHeight(worldPos, biomeWeights);
            int heightInt = (int)math.floor(terrainHeight);

            // Store height map data
            int heightMapIndex = x + z * Core.ChunkData.SIZE;
            HeightMap[heightMapIndex] = new Core.HeightPoint
            {
                height = (byte)math.clamp(heightInt, 0, 255),
                blockType = DetermineSurfaceBlock(terrainHeight, dominantBiome)
            };

            // Generate blocks for this column
            for (int y = 0; y < Core.ChunkData.HEIGHT; y++)
            {
                int blockIndex = GetBlockIndex(x, y, z);
                if (blockIndex >= 0 && blockIndex < Blocks.Length)
                {
                    Blocks[blockIndex] = DetermineBlockType(x, y, z, heightInt, terrainHeight, biomeWeights);
                }
            }
        }

        private int GetBlockIndex(int x, int y, int z)
        {
            return x + (z * Core.ChunkData.SIZE) + (y * Core.ChunkData.SIZE * Core.ChunkData.SIZE);
        }

        private float CalculateTerrainHeight(float2 position, NativeArray<float> biomeWeights)
        {
            float terrainHeight = 0f;
            for (int i = 0; i < Biomes.Length; i++)
            {
                var heightSettings = Biomes[i].HeightSettings;
                float biomeHeight = heightSettings.BaseHeight;
                biomeHeight += NoiseUtility.Sample2D(position, heightSettings.TerrainNoiseSettings) 
                              * heightSettings.HeightVariation;
                biomeHeight += heightSettings.SeaLevelOffset;
                terrainHeight += biomeHeight * biomeWeights[i];
            }
            return terrainHeight;
        }

        private byte DetermineBlockType(int x, int y, int z, int heightInt, float exactHeight, NativeArray<float> biomeWeights)
        {
            // If terrain height is disabled, use flat terrain at sea level
            int effectiveHeight = EnableTerrainHeight ? heightInt : (int)SeaLevel;

            // Check for 3D terrain generation first if enabled
            if (EnableCaves)
            {
                float3 worldPos = new float3(
                    ChunkPosition.x * Core.ChunkData.SIZE + x,
                    y,
                    ChunkPosition.z * Core.ChunkData.SIZE + z
                );

                float noise = NoiseUtility.Sample3D(worldPos, GlobalDensityNoise);
                float density = 0f;
                float totalWeight = 0f;
                
                for (int i = 0; i < Biomes.Length; i++)
                {
                    var densitySettings = Biomes[i].DensitySettings;
                    float weight = biomeWeights[i];
                    
                    if (weight > 0.01f)
                    {
                        float heightRatio = math.max(0, y - densitySettings.GradientStartHeight) / Core.ChunkData.HEIGHT;
                        float verticalGradient = math.pow(1.0f - heightRatio, densitySettings.VerticalBias);
                        float heightFactor = verticalGradient * densitySettings.HeightScale + densitySettings.HeightOffset;
                        
                        float localDensity = (noise * 2f - 1f) + densitySettings.DensityBias + heightFactor;
                        
                        density += localDensity * weight;
                        totalWeight += weight;
                    }
                }
                
                if (totalWeight > 0)
                {
                    density /= totalWeight;
                    
                    // If density is very high, force block placement even above terrain height
                    if (density > 0.5f)
                    {
                        var dominantBiome = GetDominantBiome(biomeWeights);
                        return (byte)dominantBiome.Layer1.BlockType;
                    }
                    // Create air pockets/caves where density is low
                    else if (density < -0.1f)
                    {
                        return (byte)BlockType.Air;
                    }
                }
            }

            // Regular height-based terrain generation
            if (y > effectiveHeight)
            {
                if (EnableWater && y <= SeaLevel)
                    return (byte)BlockType.Water;
                return (byte)BlockType.Air;
            }

            // Surface block
            if (y == effectiveHeight)
            {
                return DetermineSurfaceBlock(exactHeight, GetDominantBiome(biomeWeights));
            }

            // Layer blocks
            if (CheckLayer(GetDominantBiome(biomeWeights).Layer1, effectiveHeight - y, y, effectiveHeight, out byte blockType))
                return blockType;
            if (GetDominantBiome(biomeWeights).LayerCount > 1 && CheckLayer(GetDominantBiome(biomeWeights).Layer2, effectiveHeight - y, y, effectiveHeight, out blockType))
                return blockType;
            if (GetDominantBiome(biomeWeights).LayerCount > 2 && CheckLayer(GetDominantBiome(biomeWeights).Layer3, effectiveHeight - y, y, effectiveHeight, out blockType))
                return blockType;

            // Default deep blocks
            return effectiveHeight - y < DefaultLayerDepth ? DefaultSubsurfaceBlock : DefaultDeepBlock;
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
            return (byte)(isUnderwater ? biome.UnderwaterSurfaceBlock : biome.DefaultSurfaceBlock);
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
    }
}