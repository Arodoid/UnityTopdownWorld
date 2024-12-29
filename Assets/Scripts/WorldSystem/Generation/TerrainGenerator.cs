using UnityEngine;
using VoxelGame.WorldSystem.Generation;

public class TerrainGenerator
{
    // Base terrain settings
    private readonly float baseHeight = 64f;
    private readonly float baseScale = 0.03f;    // Large-scale terrain features
    private readonly float hillScale = 0.01f;    // Medium hills
    private readonly float mountainScale = 0.005f;// Mountains (very large features)
    private readonly float roughScale = 0.1f;    // Small-scale roughness
    
    // Noise strengths
    private readonly float baseStrength = 1f;     // How much the base terrain varies
    private readonly float hillStrength = 4f;    // How tall the hills are
    private readonly float mountainStrength = 32f; // How tall mountains can be
    private readonly float roughStrength = 2f;    // How rough the terrain is
    
    // Mountain threshold
    private readonly float mountainThreshold = 0.6f;
    
    // Noise offsets
    private readonly float[] offsets;

    public TerrainGenerator()
    {
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
            GenerateTerrainColumn(chunk, x, z, worldY, heightMap[x,z], biomeData.GetTemperature(x,z));
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

    private void GenerateTerrainColumn(Chunk chunk, int x, int y, int worldY, float height, float temperature)
    {
        for (int localY = 0; localY < Chunk.ChunkSize; localY++)
        {
            float worldHeight = worldY + localY;
            
            if (worldHeight <= height)
            {
                chunk.SetBlock(x, localY, y, DetermineBlockType(worldHeight, height, temperature));
            }
            else
            {
                chunk.SetBlock(x, localY, y, null); // Air
            }
        }
    }

    private Block DetermineBlockType(float worldHeight, float surfaceHeight, float temperature)
    {
        float depth = surfaceHeight - worldHeight;
        
        // Surface block determination
        if (depth <= 1)
        {
            if (temperature > 0.7f) // Desert
                return Random.value < 0.8f ? Block.Types.Sand : Block.Types.RedSand;
            else if (temperature < 0.2f) // Cold biome
                return Block.Types.SnowGrass;
            else // Normal biome
                return Block.Types.Grass;
        }
        
        // Sub-surface layers
        if (depth <= 4)
        {
            if (temperature > 0.7f) // Desert
                return Block.Types.Sand;
            else if (worldHeight < 60) // Near water level
                return Block.Types.Clay;
            else
                return Block.Types.Dirt;
        }
        
        // Deep underground
        float stoneNoise = GetNoise(worldHeight * 0.1f, surfaceHeight * 0.1f, 0, 1f);
        if (stoneNoise > 0.7f)
            return Block.Types.Granite;
        else if (stoneNoise < 0.3f)
            return Block.Types.Basalt;
        else
            return Random.value < 0.9f ? Block.Types.Stone : Block.Types.Gravel;
    }

    private float GetNoise(float x, float z, int offsetIndex, float scale)
    {
        int index = offsetIndex * 2;
        float nx = (x + offsets[index]) * scale;
        float nz = (z + offsets[index + 1]) * scale;
        return Mathf.PerlinNoise(nx, nz);
    }
}