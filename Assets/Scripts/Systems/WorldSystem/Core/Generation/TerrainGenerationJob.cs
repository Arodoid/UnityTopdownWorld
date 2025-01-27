using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using WorldSystem.Core;
using WorldSystem.Data;

namespace WorldSystem.Generation
{
    public struct TerrainGenerationJob : IJobParallelFor
    {
        [ReadOnly] public int3 ChunkPosition;
        [NativeDisableParallelForRestriction] public NativeArray<byte> Blocks;
        [NativeDisableParallelForRestriction] public NativeArray<Core.HeightPoint> HeightMap;
        
        // Terrain shape
        [ReadOnly] public NativeArray<float> NoiseValues;
        
        // Biome data
        [ReadOnly] public NativeArray<float> TemperatureMap;
        [ReadOnly] public NativeArray<float> HumidityMap;
        [ReadOnly] public NativeArray<BiomeSettings> Biomes;
        public float WaterLevel;
        
        public void Execute(int index)
        {
            int x = index % Data.ChunkData.SIZE;
            int z = index / Data.ChunkData.SIZE;
            int mapIndex = x + z * Data.ChunkData.SIZE;
            
            float noiseValue = NoiseValues[mapIndex];
            int targetHeight = (int)(noiseValue * Data.ChunkData.HEIGHT);
            targetHeight = math.clamp(targetHeight, 1, Data.ChunkData.HEIGHT - 1);
            
            var biomeGen = new BiomeGenerator(Biomes, WaterLevel) 
            { 
                TemperatureMap = TemperatureMap,
                HumidityMap = HumidityMap,
                ContinentalnessMap = NoiseValues
            };
            
            int highestSolidBlock = 0;
            for (int y = 0; y < Data.ChunkData.HEIGHT; y++)
            {
                int blockIndex = x + (z * Data.ChunkData.SIZE) + (y * Data.ChunkData.SIZE * Data.ChunkData.SIZE);

                if (y <= targetHeight)
                {
                    if (y == targetHeight)
                    {
                        BlockType surfaceBlock = biomeGen.GetSurfaceBlock(x, z, y);
                        Blocks[blockIndex] = (byte)surfaceBlock;
                    }
                    else
                    {
                        Blocks[blockIndex] = (byte)BlockType.Stone;
                    }
                    highestSolidBlock = y;
                }
                else
                {
                    // Simple: if below water level = water, otherwise air
                    Blocks[blockIndex] = y <= WaterLevel ? (byte)BlockType.Water : (byte)BlockType.Air;
                }
            }

            int heightMapIndex = x + z * Data.ChunkData.SIZE;
            HeightMap[heightMapIndex] = new Core.HeightPoint
            {
                height = (byte)highestSolidBlock,
                blockType = Blocks[x + (z * Data.ChunkData.SIZE) + (highestSolidBlock * Data.ChunkData.SIZE * Data.ChunkData.SIZE)]
            };
        }
    }
}