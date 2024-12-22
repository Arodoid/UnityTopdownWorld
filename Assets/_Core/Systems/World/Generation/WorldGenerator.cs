using UnityEngine;

/// <summary>
/// Handles terrain generation.
/// Pure generation logic - no storage or visualization.
/// </summary>
public class WorldGenerator : MonoBehaviour, IWorldSystem
{

   [Header("Terrain Settings")]
   [SerializeField] private int seed = 12345;
   [SerializeField] private float terrainScale = 0.02f;
   
   [Header("Height Settings")]
   [SerializeField] private int baseHeight = 32;
   [SerializeField] private float heightVariation = 20f;
    private FastNoiseLite noise;
    public void Initialize()
   {
       noise = new FastNoiseLite(seed);
   }
    public void Cleanup() { }
    // Core terrain generation - chunk independent
   public int GetHeightAt(int worldX, int worldZ)
   {
       float noiseValue = noise.GetNoise(worldX, worldZ);
       return baseHeight + Mathf.RoundToInt(noiseValue * heightVariation);
   }
    public byte GetBlockAt(int worldX, int worldY, int worldZ)
   {
       int terrainHeight = GetHeightAt(worldX, worldZ);
        if (worldY > terrainHeight)
           return BlockType.Air.ID;
       
       if (worldY == terrainHeight)
           return BlockType.Grass.ID;
       
       if (worldY > terrainHeight - 4)
           return BlockType.Dirt.ID;
       
       return BlockType.Stone.ID;
   }

   // Add a method to generate an entire chunk at once
   public void GenerateChunk(ChunkData chunk)
   {
       Vector3Int chunkPos = chunk.Position;
       int worldX = chunkPos.x * ChunkData.CHUNK_SIZE;
       int worldY = chunkPos.y * ChunkData.CHUNK_SIZE;
       int worldZ = chunkPos.z * ChunkData.CHUNK_SIZE;

       // Generate height map for the entire chunk at once
       float[,] heightMap = new float[ChunkData.CHUNK_SIZE, ChunkData.CHUNK_SIZE];
       for (int x = 0; x < ChunkData.CHUNK_SIZE; x++)
       {
           for (int z = 0; z < ChunkData.CHUNK_SIZE; z++)
           {
               heightMap[x,z] = GetHeightAt(worldX + x, worldZ + z);
           }
       }

       // Fill chunk using the pre-calculated height map
       for (int x = 0; x < ChunkData.CHUNK_SIZE; x++)
       {
           for (int z = 0; z < ChunkData.CHUNK_SIZE; z++)
           {
               int terrainHeight = Mathf.RoundToInt(heightMap[x,z]);
               
               for (int y = 0; y < ChunkData.CHUNK_SIZE; y++)
               {
                   int currentHeight = worldY + y;
                   
                   if (currentHeight > terrainHeight)
                       continue; // Skip air blocks (they're not stored)
                       
                   byte blockType;
                   if (currentHeight == terrainHeight)
                       blockType = BlockType.Grass.ID;
                   else if (currentHeight > terrainHeight - 4)
                       blockType = BlockType.Dirt.ID;
                   else
                       blockType = BlockType.Stone.ID;
                       
                   chunk.SetBlock(new Vector3Int(x, y, z), blockType);
               }
           }
       }
   }
}

/// <summary>
/// Fast Noise Lite implementation would go here.
/// You'll need to implement or import a noise generation library.
/// </summary>
public class FastNoiseLite
{
    private int seed;
    private const float NOISE_SCALE = 0.02f;  // Smaller = more gradual changes

    public FastNoiseLite(int seed)
    {
        this.seed = seed;
        Random.InitState(seed);
    }

    public float GetNoise(float x, float z)
    {
        // Offset by seed for different patterns
        float offsetX = x + seed;
        float offsetZ = z + seed;

        // Layer multiple noise octaves for more natural terrain
        float noise = 0f;
        float amplitude = 1f;
        float frequency = NOISE_SCALE;
        float persistence = 0.5f;

        for (int i = 0; i < 4; i++)
        {
            noise += Mathf.PerlinNoise(offsetX * frequency, offsetZ * frequency) * amplitude;
            amplitude *= persistence;
            frequency *= 2f;
        }

        // Normalize to 0-1 range
        return noise / 2f;
    }
} 