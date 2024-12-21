using UnityEngine;

public class WorldGenerator : MonoBehaviour
{
    [SerializeField] private int seed = 12345;
    [SerializeField] private float terrainNoiseScale = 0.1f;  // For base terrain
    [SerializeField] private float dirtPatchNoiseScale = 0.05f;  // For dirt patches
    [SerializeField] private float dirtThreshold = 0.6f;  // Higher = less dirt patches
    
    private System.Random random;

    private void Awake()
    {
        random = new System.Random(seed);
        Debug.Log($"World Generator initialized with seed: {seed}");
    }

    // Add this to track generation progress
    private int chunksGenerated = 0;
    public void GenerateChunk(ChunkData chunk)
    {
        if (chunk.IsGenerated) return;

        Vector3Int worldPos = chunk.ChunkPosition;
        
        for (int x = 0; x < ChunkData.CHUNK_SIZE; x++)
        {
            for (int z = 0; z < ChunkData.CHUNK_SIZE; z++)
            {
                float worldX = (worldPos.x * ChunkData.CHUNK_SIZE + x);
                float worldZ = (worldPos.y * ChunkData.CHUNK_SIZE + z);
                
                // Base terrain height noise
                float heightNoise = Mathf.PerlinNoise(
                    worldX * terrainNoiseScale + seed, 
                    worldZ * terrainNoiseScale + seed
                );
                
                // Dirt patch noise (different frequency and offset)
                float dirtNoise = Mathf.PerlinNoise(
                    worldX * dirtPatchNoiseScale + seed * 2, 
                    worldZ * dirtPatchNoiseScale + seed * 2
                );

                // Calculate terrain height (8-12 blocks)
                int terrainHeight = Mathf.FloorToInt(8 + heightNoise * 4);
                
                for (int y = 0; y < ChunkData.CHUNK_SIZE; y++)
                {
                    if (y < terrainHeight - 4)
                    {
                        // Deep underground - always stone
                        chunk.SetBlock(x, y, z, BlockWorld.STONE);
                    }
                    else if (y < terrainHeight)
                    {
                        // Underground - dirt
                        chunk.SetBlock(x, y, z, BlockWorld.DIRT);
                    }
                    else if (y == terrainHeight)
                    {
                        // Surface block - mix of grass and dirt based on noise
                        byte surfaceBlock = (dirtNoise > dirtThreshold) 
                            ? BlockWorld.DIRT 
                            : BlockWorld.GRASS;
                        chunk.SetBlock(x, y, z, surfaceBlock);
                    }
                }
            }
        }
        
        chunk.IsGenerated = true;
        chunksGenerated++;
        
        if (chunksGenerated == 1 || chunksGenerated % 100 == 0)
        {
            Debug.Log($"Generated chunk at {chunk.ChunkPosition}");
        }
    }
} 