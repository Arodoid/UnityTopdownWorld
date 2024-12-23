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

   [Header("Gravel Settings")]
   [SerializeField] private float gravelScale = 0.05f;
   [SerializeField] private float gravelThreshold = 0.6f;
   [SerializeField] private int gravelMaxDepth = 3;

   private FastNoiseLite gravelNoise;

   public void Initialize()
   {
       noise = new FastNoiseLite(seed);
       gravelNoise = new FastNoiseLite(seed + 1); // Different seed for variation
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
       {
           // Check for surface gravel
           float gravelValue = gravelNoise.GetNoise(worldX, worldZ);
           if (gravelValue > gravelThreshold)
               return BlockType.Gravel.ID;
           return BlockType.Grass.ID;
       }
       
       if (worldY > terrainHeight - 4)
       {
           // Check for underground gravel patches
           float gravelValue = gravelNoise.GetNoise(worldX, worldY * 0.5f, worldZ);
           if (gravelValue > gravelThreshold && terrainHeight - worldY < gravelMaxDepth)
               return BlockType.Gravel.ID;
           return BlockType.Dirt.ID;
       }
       
       return BlockType.Stone.ID;
   }

   // Add a method to generate an entire chunk at once
   public void GenerateChunk(ChunkData chunk)
   {
       Vector3Int chunkPos = chunk.Position;
       int worldX = chunkPos.x * ChunkData.CHUNK_SIZE;
       int worldY = chunkPos.y * ChunkData.CHUNK_SIZE;
       int worldZ = chunkPos.z * ChunkData.CHUNK_SIZE;

       // Quick height check first - if chunk is entirely above or below terrain, skip detailed check
       int maxHeight = GetMaxHeightInChunk(worldX, worldZ);
       int minY = worldY;
       int maxY = worldY + ChunkData.CHUNK_SIZE;

       // Quick empty check
       if (maxY < maxHeight - 4) // Definitely has blocks (underground)
       {
           chunk.MarkNotEmpty();
       }
       else if (minY > maxHeight) // Definitely empty (above terrain)
       {
           chunk.MarkEmpty();
           return; // Skip generation
       }

       // Only do detailed generation if we're in the transition zone
       for (int x = 0; x < ChunkData.CHUNK_SIZE; x++)
       {
           for (int z = 0; z < ChunkData.CHUNK_SIZE; z++)
           {
               int height = GetHeightAt(worldX + x, worldZ + z);
               
               for (int y = 0; y < ChunkData.CHUNK_SIZE; y++)
               {
                   int currentY = worldY + y;
                   if (currentY > height)
                       continue;

                   byte blockType = GetBlockAt(worldX + x, currentY, worldZ + z);
                   if (blockType != BlockType.Air.ID)
                   {
                       chunk.SetBlock(new Vector3Int(x, y, z), blockType);
                   }
               }
           }
       }
   }

   private int GetMaxHeightInChunk(int worldX, int worldZ)
   {
       int maxHeight = int.MinValue;
       
       // Check corners and center
       maxHeight = Mathf.Max(maxHeight, GetHeightAt(worldX, worldZ));
       maxHeight = Mathf.Max(maxHeight, GetHeightAt(worldX + ChunkData.CHUNK_SIZE, worldZ));
       maxHeight = Mathf.Max(maxHeight, GetHeightAt(worldX, worldZ + ChunkData.CHUNK_SIZE));
       maxHeight = Mathf.Max(maxHeight, GetHeightAt(worldX + ChunkData.CHUNK_SIZE, worldZ + ChunkData.CHUNK_SIZE));
       maxHeight = Mathf.Max(maxHeight, GetHeightAt(worldX + ChunkData.CHUNK_SIZE/2, worldZ + ChunkData.CHUNK_SIZE/2));
       
       return maxHeight;
   }
}

/// <summary>
/// Fast Noise Lite implementation would go here.
/// You'll need to implement or import a noise generation library.
/// </summary>
public class FastNoiseLite
{
    private int seed;
    private const float NOISE_SCALE = 0.02f;

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

    // Add 3D noise method
    public float GetNoise(float x, float y, float z)
    {
        // Create pseudo-3D noise by combining multiple 2D noise samples
        float xy = GetNoise(x, y);
        float yz = GetNoise(y, z);
        float xz = GetNoise(x, z);
        
        // Combine the noise samples
        return (xy + yz + xz) / 3f;
    }
} 