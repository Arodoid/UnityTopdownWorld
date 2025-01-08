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

        [ReadOnly] public float waterLevel;
        [ReadOnly] public float oceanThreshold;
        [ReadOnly] public float mountainThreshold;
        [ReadOnly] public float forestThreshold;
        [ReadOnly] public float mountainHeight;
        [ReadOnly] public float erosionStrength;
        [ReadOnly] public float erosionDetailInfluence;

        [ReadOnly] public float localVariationScale;
        [ReadOnly] public float localVariationStrength;

        [ReadOnly] public float continentScale;
        [ReadOnly] public float temperatureScale;
        [ReadOnly] public float moistureScale;
        [ReadOnly] public float weirdnessScale;
        [ReadOnly] public float erosionScale;
        [ReadOnly] public float mountainVariationStrength;
        [ReadOnly] public float forestVariationStrength;
        [ReadOnly] public float plainsVariationStrength;

        [ReadOnly] public int oceanFloorMin;
        [ReadOnly] public int oceanFloorMax;

        private const int WATER_LEVEL = 64;
        private const int OCEAN_FLOOR_MIN = 20;
        private const int OCEAN_FLOOR_MAX = 30;

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
            
            // Use proper scales for noise sampling
            float2 scaledPos = worldPos;
            float oceanFloorHeight = math.lerp(
                OCEAN_FLOOR_MIN, 
                OCEAN_FLOOR_MAX, 
                WorldNoise.Sample(scaledPos, continentScale, seed + 500)
            );
            
            // Base height calculation with proper scaling
            float baseHeight;
            if (biome.continentalness < oceanThreshold)
            {
                float oceanDepthFactor = 1 - (biome.continentalness / oceanThreshold);
                baseHeight = math.lerp(waterLevel, oceanFloorHeight, oceanDepthFactor);
            }
            else
            {
                float landHeightFactor = (biome.continentalness - oceanThreshold) / (1 - oceanThreshold);
                baseHeight = math.lerp(waterLevel, mountainHeight, landHeightFactor);
            }

            // Add biome-specific variations with proper strength values
            float biomeVariation = 0;
            if (biome.GetMountainWeight() > mountainThreshold)
            {
                biomeVariation = WorldNoise.Sample(scaledPos, weirdnessScale, seed + 1) * mountainVariationStrength;
            }
            else if (biome.GetForestWeight() > forestThreshold)
            {
                biomeVariation = WorldNoise.Sample(scaledPos, weirdnessScale, seed + 2) * forestVariationStrength;
            }
            else
            {
                biomeVariation = WorldNoise.Sample(scaledPos, weirdnessScale, seed + 3) * plainsVariationStrength;
            }

            // Add local variation with proper scale
            float localVariation = WorldNoise.Sample(scaledPos, localVariationScale, seed) * localVariationStrength;
            
            // Add erosion influence
            float erosion = WorldNoise.Sample(scaledPos, erosionScale, seed + 4);
            float erosionModifier = math.lerp(1f, 1f - erosionStrength, erosion);
            
            // Calculate final height
            int finalHeight = (int)math.clamp(
                (baseHeight + biomeVariation + localVariation) * erosionModifier,
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
                // Ensure ocean areas are properly filled with water
                if (y <= WATER_LEVEL && biome.continentalness < oceanThreshold)
                    return BlockType.Water;
                return BlockType.Air;
            }
            
            if (y == height)
            {
                // Underwater blocks
                if (y < WATER_LEVEL && biome.continentalness < oceanThreshold)
                {
                    return (y > WATER_LEVEL - 5) ? BlockType.Sand : BlockType.Gravel;
                }
                
                // Mountain peaks
                if (biome.continentalness > 0.7f &&y > 90)
                {
                    return (y > 100) ? BlockType.Snow: BlockType.Stone;
                }
                
                // Coastal areas
                if (math.abs(y - WATER_LEVEL) <= 2)
                    return BlockType.Sand;
                    
                // Normal terrain - vary based on temperature and height
                if (y > 80)
                    return BlockType.Stone;
                if (biome.temperature < 0.2f)
                    return BlockType.Snow;
                if (biome.moisture > 0.6f)
                    return BlockType.Grass;
                if (biome.temperature > 0.7f)
                    return BlockType.Sand;
                return BlockType.Grass;
            }
            
            // Underground
            if (y > height - 3)
                return BlockType.Dirt;
            return BlockType.Stone;
        }
    }
} 