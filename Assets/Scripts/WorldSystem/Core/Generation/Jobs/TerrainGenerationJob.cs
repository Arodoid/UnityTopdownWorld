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
            // Water fills everything above ocean floor up to water level in ocean biomes
            if (y > height)
            {
                if (biome.continentalness < OCEAN_THRESHOLD && y <= WATER_LEVEL)
                    return BlockType.Water;
                return BlockType.Air;
            }
            
            if (y == height)
            {
                if (biome.continentalness < OCEAN_THRESHOLD)
                    return BlockType.Sand;  // Ocean floor
                if (y <= WATER_LEVEL + 2)
                    return BlockType.Sand;  // Beach
                if (biome.temperature < 0.2f)
                    return BlockType.Snow;
                return BlockType.Grass;
            }
            
            if (y >= height - 3)
                return biome.continentalness < 0.4f ? BlockType.Sand : BlockType.Dirt;
            
            return BlockType.Stone;
        }

        private float GetBiomeHeight(BiomeData biome)
        {
            // Mountains
            float mountainWeight = math.smoothstep(0.6f, 0.8f, biome.continentalness) 
                * math.smoothstep(0.3f, 0.0f, biome.temperature);
            float mountainHeight = 30 * mountainWeight * (biome.weirdness * 0.5f + 0.5f);

            // Plains
            float plainsWeight = math.smoothstep(0.3f, 0.7f, biome.moisture) 
                * math.smoothstep(0.3f, 0.7f, biome.temperature);
            float plainsHeight = 5 * plainsWeight;

            // Desert
            float desertWeight = math.smoothstep(0.7f, 1f, biome.temperature) 
                * math.smoothstep(0.3f, 0.0f, biome.moisture);
            float desertHeight = 8 * desertWeight * (math.abs(biome.weirdness) * 0.7f + 0.3f);

            return math.max(math.max(mountainHeight, plainsHeight), desertHeight);
        }
    }
} 