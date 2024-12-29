using UnityEngine;
using System.Threading.Tasks;
using VoxelGame.WorldSystem.Generation;

public class WorldGenerator : MonoBehaviour, IChunkGenerator
{
    private TerrainGenerator terrainGenerator;
    private BiomeGenerator biomeGenerator;
    private FeatureGenerator featureGenerator;

    private void Awake()
    {
        terrainGenerator = new TerrainGenerator();
        biomeGenerator = new BiomeGenerator();
        featureGenerator = new FeatureGenerator();
    }

    public Chunk GenerateChunk(Vector3Int position)
    {
        Chunk chunk = new Chunk(position);
        
        // Get biome data first as it influences other generation
        var biomeData = biomeGenerator.GenerateChunkBiomeData(position);
        
        // Generate terrain using biome data
        var heightMap = terrainGenerator.GenerateChunkTerrain(chunk, biomeData);
        
        // Add features last
        featureGenerator.PopulateChunkFeatures(chunk, position, heightMap, biomeData);
        
        return chunk;
    }

    public async Task<Chunk> GenerateChunkAsync(Vector3Int position)
    {
        return await Task.Run(() =>
        {
            return GenerateChunk(position);
        });
    }
} 