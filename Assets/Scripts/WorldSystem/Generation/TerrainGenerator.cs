using UnityEngine;
using VoxelGame.WorldSystem.Generation.Core;
using VoxelGame.WorldSystem.Biomes;  // For BiomeData
// using VoxelGame.WorldSystem.Biomes.BiomeRegistry;

namespace VoxelGame.WorldSystem.Generation.Terrain
{
    public class TerrainGenerator
    {
        private readonly NoiseGenerator noiseGenerator;
        // Base terrain settings
        private readonly float baseHeight = 64f;
        private readonly float baseScale = 0.03f;    // Large-scale terrain features
        private readonly float hillScale = 0.01f;    // Medium hills
        private readonly float mountainScale = 0.005f;// Mountains (very large features)
        private readonly float roughScale = 0.1f;    // Small-scale roughness
        // Noise strengths
        private readonly float baseStrength = 0.5f;     // How much the base terrain varies
        private readonly float hillStrength = 2f;    // How tall the hills are
        private readonly float mountainStrength = 16f; // How tall mountains can be
        private readonly float roughStrength = 2f;    // How rough the terrain is
        
        // Mountain threshold
        private readonly float mountainThreshold = 0.6f;
        
        // Noise offsets
        private readonly float[] offsets;

        public TerrainGenerator(NoiseGenerator noiseGenerator)
        {
            this.noiseGenerator = noiseGenerator;
            offsets = new float[8]; // 2 offsets each for base, hills, mountains, and roughness
            for (int i = 0; i < offsets.Length; i++)
            {
                offsets[i] = Random.Range(-10000f, 10000f);
            }
        }

        public float[,] GenerateChunkTerrain(Chunk chunk, BiomeData biomeData)
        {
            float[,] heightMap = new float[Chunk.ChunkSize, Chunk.ChunkSize];
            int worldY = chunk.Position.y * Chunk.ChunkSize;

            for (int x = 0; x < Chunk.ChunkSize; x++)
            for (int z = 0; z < Chunk.ChunkSize; z++)
            {
                float worldX = chunk.Position.x * Chunk.ChunkSize + x;
                float worldZ = chunk.Position.z * Chunk.ChunkSize + z;

                // Calculate height
                heightMap[x,z] = CalculateHeight(worldX, worldZ, biomeData.GetTemperature(x,z));
                
                // Add roughness
                heightMap[x,z] += CalculateRoughness(worldX, worldZ);
                
                // Round to nearest integer for block-like appearance
                heightMap[x,z] = Mathf.Round(heightMap[x,z]);
                
                // Generate terrain column
                GenerateTerrainColumn(chunk, x, z, worldY, heightMap[x,z], biomeData.GetTemperature(x,z), biomeData);
            }

            return heightMap;
        }

        private float CalculateHeight(float worldX, float worldZ, float temperature)
        {
            // Base terrain (gentle rolling hills)
            float baseNoise = GetNoise(worldX, worldZ, 0, baseScale);
            float height = baseHeight + (baseNoise * baseStrength);

            // Medium hills
            float hillNoise = GetNoise(worldX, worldZ, 1, hillScale);
            height += hillNoise * hillStrength;

            // Large mountains
            float mountainNoise = GetNoise(worldX, worldZ, 2, mountainScale);
            if (mountainNoise > mountainThreshold)
            {
                float mountainFactor = (mountainNoise - mountainThreshold) / (1 - mountainThreshold);
                height += mountainFactor * mountainStrength;
            }

            // Smooth biome height modifications
            float desertTransition = Mathf.SmoothStep(0f, 1f, (temperature - 0.6f) / 0.2f);
            float snowTransition = Mathf.SmoothStep(0f, 1f, (0.3f - temperature) / 0.2f);
            
            // Blend between different biome heights
            float desertHeight = height * 0.8f;  // Desert is 20% lower
            float normalHeight = height;
            float snowHeight = height * 1.1f;    // Snow biomes slightly higher

            // Smoothly interpolate between heights
            height = Mathf.Lerp(normalHeight, desertHeight, desertTransition);
            height = Mathf.Lerp(height, snowHeight, snowTransition);

            return height;
        }

        private float CalculateRoughness(float worldX, float worldZ)
        {
            // Get rough noise
            float roughNoise = GetNoise(worldX, worldZ, 3, roughScale);
            
            // Convert to -1 to 1 range
            roughNoise = (roughNoise * 2) - 1;
            
            // Apply strength
            return roughNoise * roughStrength;
        }

        private void GenerateTerrainColumn(Chunk chunk, int x, int z, int chunkWorldY, float heightY, float temperature, BiomeData biomeData)
        {
            // Convert world height to local chunk coordinates
            float localMaxHeight = heightY - chunkWorldY;

            for (int y = 0; y < Chunk.ChunkSize; y++)
            {
                // Skip if this y-level is outside our chunk's height range
                if (y > localMaxHeight)
                {
                    chunk.SetBlock(x, y, z, null); // Air
                    continue;
                }

                chunk.SetBlock(x, y, z, 
                    DetermineBlockType(chunkWorldY + y, heightY, temperature, biomeData, x, z));
            }
        }

        private Block DetermineBlockType(float worldHeight, float surfaceHeight, float temperature, BiomeData biomeData, int x, int z)
        {
            float depth = surfaceHeight - worldHeight;
            
            // Get the biome settings for this location
            BiomeType biomeType = biomeData.GetBiomeType(x, z);
            BiomeSettings biomeSettings = BiomeRegistry.GetSettings(biomeType);
            
            // Surface block determination (top layer)
            if (depth <= 1)
            {
                return biomeSettings.SurfaceBlock;
            }
            
            // Subsurface layers (0-4 blocks deep)
            if (depth <= biomeSettings.SubsurfaceDepth)
            {
                return biomeSettings.SubsurfaceBlock;
            }
            
            // Deep underground
            if (worldHeight < 20)
            {
                return Block.Types.Stone;
            }
            
            // Random stone or gravel (95% stone, 5% gravel)
            return Random.value < 0.95f ? Block.Types.Stone : Block.Types.Stone;
        }

        private float GetNoise(float x, float z, int offsetIndex, float scale)
        {
            return noiseGenerator.GetNoise(x, z, offsetIndex, scale);
        }
    }
}