using UnityEngine;
using VoxelGame.WorldSystem.Generation;

public class BiomeGenerator
{
    private readonly float temperatureScale = 0.02f; // Increased for larger biomes
    private readonly float temperatureOffset = 0.5f; // Center the temperature range
    private readonly float[] offsets;

    public BiomeGenerator()
    {
        offsets = new float[2];
        for (int i = 0; i < offsets.Length; i++)
        {
            offsets[i] = Random.Range(-10000f, 10000f);
        }
    }

    public BiomeData GenerateChunkBiomeData(Vector3Int chunkPosition)
    {
        var biomeData = new BiomeData(Chunk.ChunkSize);
        
        for (int x = 0; x < Chunk.ChunkSize; x++)
        for (int z = 0; z < Chunk.ChunkSize; z++)
        {
            float worldX = chunkPosition.x * Chunk.ChunkSize + x;
            float worldZ = chunkPosition.z * Chunk.ChunkSize + z;
            
            float temperature = GetNoise(worldX, worldZ);
            biomeData.SetBiomeData(x, z, temperature);
        }
        
        return biomeData;
    }

    public BiomeType GetBiomeAt(Vector3 worldPosition)
    {
        float temperature = GetNoise(worldPosition.x, worldPosition.z);
        return temperature > 0.7f ? BiomeType.Desert : BiomeType.Grassland;
    }

    private float GetNoise(float x, float z)
    {
        float nx = (x + offsets[0]) * temperatureScale;
        float nz = (z + offsets[1]) * temperatureScale;
        // Adjust the range to be more balanced
        return (Mathf.PerlinNoise(nx, nz) + temperatureOffset) * 0.5f;
    }
}