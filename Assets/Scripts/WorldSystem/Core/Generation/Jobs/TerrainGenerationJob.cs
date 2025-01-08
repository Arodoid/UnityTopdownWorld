using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using WorldSystem.Data;

namespace WorldSystem.Generation.Jobs
{
    [BurstCompile]
    public struct TerrainGenerationJob : IJobParallelFor
    {
        [ReadOnly] public int3 position;
        [ReadOnly] public int seed;
        [ReadOnly] public NativeArray<BiomeData> biomeData;
        [NativeDisableParallelForRestriction]
        public NativeArray<byte> blocks;

        private const float LOCAL_VARIATION_SCALE = 0.01f;
        private const int WATER_LEVEL = 64;
        private const int OCEAN_FLOOR_MIN = 20;
        private const int OCEAN_FLOOR_MAX = 30;
        private const float OCEAN_THRESHOLD = 0.4f;

        public void Execute(int index)
        {
            int x = index % ChunkData.SIZE;
            int z = index / ChunkData.SIZE;
            int biomeIndex = z * ChunkData.SIZE + x;

            float2 worldPos = new float2(
                position.x * ChunkData.SIZE + x,
                position.z * ChunkData.SIZE + z
            );

            BiomeData biome = biomeData[biomeIndex];
            
            // Ocean floor has its own variation
            float oceanFloorHeight = math.lerp(
                OCEAN_FLOOR_MIN, 
                OCEAN_FLOOR_MAX, 
                WorldNoise.Sample(worldPos, 0.03f, seed + 500)
            );
            
            // Base height now considers water level
            float baseHeight;
            if (biome.continentalness < OCEAN_THRESHOLD)
            {
                // Ocean depth gradually increases as continentalness decreases
                float oceanDepthFactor = 1 - (biome.continentalness / OCEAN_THRESHOLD);
                baseHeight = math.lerp(WATER_LEVEL, oceanFloorHeight, oceanDepthFactor);
            }
            else
            {
                // Land height starts at water level and goes up
                float landHeightFactor = (biome.continentalness - OCEAN_THRESHOLD) / (1 - OCEAN_THRESHOLD);
                baseHeight = math.lerp(WATER_LEVEL, 100, landHeightFactor);
            }

            // Add biome-specific variation
            float biomeHeight = GetBiomeHeight(biome);
            
            // Add local variation
            float localVariation = WorldNoise.Sample(worldPos, LOCAL_VARIATION_SCALE, seed) * 15;
            
            // Clamp height to valid range
            int finalHeight = (int)math.clamp(
                baseHeight + biomeHeight + localVariation,
                0,
                ChunkData.HEIGHT - 1
            );

            // Fill column for this x,z coordinate
            for (int y = 0; y < ChunkData.HEIGHT; y++)
            {
                int blockIndex = GetBlockIndex(x, y, z);
                blocks[blockIndex] = (byte)GetBlockType(y, finalHeight, biome);
            }
        }

        private int GetBlockIndex(int x, int y, int z)
        {
            return (y * ChunkData.SIZE * ChunkData.SIZE) + (z * ChunkData.SIZE) + x;
        }

        private BlockType GetBlockType(int y, int height, BiomeData biome)
        {
            if (y > height)
            {
                if (y <= WATER_LEVEL && biome.continentalness < 0.4f)
                    return BlockType.Water;
                return BlockType.Air;
            }
            
            if (y == height)
            {
                // Underwater blocks
                if (y < WATER_LEVEL && biome.continentalness < 0.4f)
                {
                    return (y > WATER_LEVEL - 5) ? BlockType.Sand : BlockType.Stone;
                }
                
                // Mountain peaks and cliffs
                if (biome.continentalness > 0.7f)
                {
                    if (y > 100)
                        return BlockType.Snow;
                    return BlockType.Stone;
                }
                
                // Coastal areas
                if (math.abs(y - WATER_LEVEL) <= 2)
                    return BlockType.Sand;
                    
                // Normal terrain
                return (y > 80) ? BlockType.Stone : BlockType.Grass;
            }
            
            // Underground
            if (y > height - 3)
                return BlockType.Dirt;
            return BlockType.Stone;
        }

        private float GetBiomeHeight(BiomeData biome)
        {
            const float DEEP_OCEAN_THRESHOLD = 0.3f;
            const float OCEAN_THRESHOLD = 0.4f;
            const float MOUNTAIN_THRESHOLD = 0.7f;
            
            // Ocean depth calculation
            if (biome.continentalness < OCEAN_THRESHOLD)
            {
                // Create deeper oceans with some variation in depth
                float oceanDepth = math.lerp(20f, WATER_LEVEL - 10,
                    math.smoothstep(0f, OCEAN_THRESHOLD, biome.continentalness));
                    
                // Add underwater terrain variation
                float underwaterNoise = biome.weirdness * 8f;
                
                // Create deeper trenches in deep ocean
                if (biome.continentalness < DEEP_OCEAN_THRESHOLD)
                {
                    float trenchDepth = 15f * math.smoothstep(DEEP_OCEAN_THRESHOLD, 0f, biome.continentalness);
                    return oceanDepth - trenchDepth + underwaterNoise;
                }
                
                return oceanDepth + underwaterNoise;
            }
            
            // Land height calculation (similar to before but adjusted thresholds)
            float mountainWeight = math.smoothstep(0.65f, 0.75f, biome.continentalness) 
                * math.smoothstep(0.3f, 0.0f, biome.temperature);
            
            float mountainBase = 120f;
            float valleyBase = WATER_LEVEL + 5f; // Start valleys slightly above water level
            
            float heightVariation = math.pow(math.abs(biome.weirdness), 0.25f);
            float mountainHeight = mountainBase * heightVariation;
            float valleyDepth = 60f * mountainWeight * biome.erosion;
            
            float finalHeight;
            
            if (biome.continentalness > MOUNTAIN_THRESHOLD) // Mountain region
            {
                finalHeight = mountainHeight - valleyDepth;
            }
            else if (biome.continentalness < OCEAN_THRESHOLD + 0.1f) // Coastal region
            {
                // Smooth transition from ocean to land
                float coastalFactor = math.smoothstep(
                    OCEAN_THRESHOLD, 
                    OCEAN_THRESHOLD + 0.1f, 
                    biome.continentalness
                );
                finalHeight = math.lerp(WATER_LEVEL, WATER_LEVEL + 15f, coastalFactor);
            }
            else // Normal terrain
            {
                float transitionFactor = math.smoothstep(
                    OCEAN_THRESHOLD + 0.1f, 
                    MOUNTAIN_THRESHOLD, 
                    biome.continentalness
                );
                finalHeight = math.lerp(
                    valleyBase,
                    mountainHeight - valleyDepth,
                    transitionFactor
                );
            }
            
            // Add minimal noise to flat areas, more to mountains
            float detailNoise = biome.weirdness * (biome.continentalness > 0.7f ? 15f : 2f);
            
            return finalHeight + detailNoise;
        }
    }
} 