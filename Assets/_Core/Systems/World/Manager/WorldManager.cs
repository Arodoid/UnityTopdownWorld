using UnityEngine;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// Central coordinator for all world operations.
/// All external systems must go through WorldManager to interact with the world.
/// </summary>
public class WorldManager : MonoBehaviour
{
    private static WorldManager instance;
    public static WorldManager Instance => instance;

    private ChunkManager chunkManager;
    private WorldGenerator worldGenerator;
    private VisualizationManager visualizationManager;

    [Header("World Settings")]
    [SerializeField] private int worldSeed = 12345;
    
    private HashSet<Vector3Int> activeChunks = new HashSet<Vector3Int>();

    private Vector3 debugBoxCenter;
    private Vector3 debugBoxSize;

    private Queue<Vector3Int> chunkLoadQueue = new Queue<Vector3Int>();
    private HashSet<Vector3Int> chunksInProgress = new HashSet<Vector3Int>();
    [SerializeField] private int chunksPerFrame = 2;

    // Track coroutines by chunk position
    private Dictionary<Vector3Int, Coroutine> activeCoroutines = new Dictionary<Vector3Int, Coroutine>();

    private void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);

        // Automatically find and assign subsystems
        chunkManager = GetComponent<ChunkManager>();
        worldGenerator = GetComponent<WorldGenerator>();
        visualizationManager = GetComponent<VisualizationManager>();
    }

    private void Start()
    {
        InitializeSystems();
    }

    private void InitializeSystems()
    {
        if (!ValidateSystems()) return;

        worldGenerator.Initialize();
        chunkManager.Initialize();
        visualizationManager.Initialize();
    }

    private bool ValidateSystems()
    {
        if (chunkManager == null || worldGenerator == null || visualizationManager == null)
        {
            Debug.LogError("Required systems not found on WorldManager!");
            return false;
        }
        return true;
    }

    private void Update()
    {
        ProcessChunkQueue();
    }

    private void ProcessChunkQueue()
    {
        int chunksThisFrame = 0;
        
        // Create a temporary list of chunks to remove from queue
        List<Vector3Int> chunksToSkip = new List<Vector3Int>();
        
        while (chunkLoadQueue.Count > 0 && chunksThisFrame < chunksPerFrame)
        {
            Vector3Int chunkPos = chunkLoadQueue.Peek(); // Peek instead of Dequeue
            
            // Skip if chunk is no longer needed
            if (!activeChunks.Contains(chunkPos))
            {
                chunkLoadQueue.Dequeue(); // Remove from queue
                continue;
            }
            
            if (!chunksInProgress.Contains(chunkPos))
            {
                chunkLoadQueue.Dequeue(); // Only dequeue if we're actually processing it
                StartCoroutine(LoadChunkAsync(chunkPos));
                chunksThisFrame++;
            }
        }
    }

    // Public API for external systems

    /// <summary>
    /// Request chunk generation and visualization for an area, cleaning up old chunks
    /// </summary>
    public void RequestChunkArea(Vector2Int center, int radius, Vector3 cameraPos)
    {
        HashSet<Vector3Int> newActiveChunks = new HashSet<Vector3Int>();
        List<ChunkRequest> requests = new List<ChunkRequest>();

        // Debug visualization of the request area
        Vector3 debugCenter = new Vector3(
            center.x * ChunkData.CHUNK_SIZE,
            ChunkData.WORLD_HEIGHT / 2,  // Center vertically
            center.y * ChunkData.CHUNK_SIZE
        );
        
        // Draw debug box for the requested area
        Vector3 size = new Vector3(
            radius * 2 * ChunkData.CHUNK_SIZE,
            ChunkData.WORLD_HEIGHT,
            radius * 2 * ChunkData.CHUNK_SIZE
        );
        
        // Align debug box with camera position
        debugBoxCenter = new Vector3(cameraPos.x, ChunkData.WORLD_HEIGHT / 2, cameraPos.z);
        debugBoxSize = size;

        // Calculate vertical chunk range for the entire world height
        int minY = 0;
        int maxY = ChunkData.WORLD_HEIGHT / ChunkData.CHUNK_SIZE;

        // Generate sorted list of chunk requests
        for (int x = -radius; x <= radius; x++)
        {
            for (int z = -radius; z <= radius; z++)
            {
                for (int y = minY; y < maxY; y++)
                {
                    Vector3Int chunkPos = new Vector3Int(center.x + x, y, center.y + z);
                    float distance = Vector3.Distance(
                        new Vector3(chunkPos.x, 0, chunkPos.z),
                        new Vector3(center.x, 0, center.y)
                    );
                    requests.Add(new ChunkRequest(chunkPos, distance));
                }
            }
        }

        // Sort by distance and queue
        requests.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        foreach (var request in requests)
        {
            newActiveChunks.Add(request.Position);
            chunkLoadQueue.Enqueue(request.Position);
        }

        // Cleanup old chunks
        foreach (Vector3Int oldChunk in activeChunks)
        {
            if (!newActiveChunks.Contains(oldChunk))
            {
                CleanupChunk(oldChunk);
            }
        }

        activeChunks = newActiveChunks;
    }

    private void CleanupChunk(Vector3Int chunkPos)
    {
        // Remove from processing queue if it's there
        chunksInProgress.Remove(chunkPos);
        
        // Remove visual representation
        visualizationManager.RemoveChunkMesh(chunkPos);
        
        // Remove chunk data
        chunkManager.MarkChunkInactive(chunkPos);
        
        // Cancel any pending coroutines for this chunk
        StopCoroutineForChunk(chunkPos);
    }

    /// <summary>
    /// Request a single chunk to be generated and visualized
    /// </summary>
    public void RequestChunk(Vector3Int chunkPos)
    {
        // Get or create chunk at this 3D position
        ChunkData chunk = chunkManager.GetChunkData(chunkPos);
        
        if (chunk == null)
        {
            chunk = new ChunkData(chunkPos);
            
            // Fill chunk with blocks
            for (int x = 0; x < ChunkData.CHUNK_SIZE; x++)
            {
                for (int z = 0; z < ChunkData.CHUNK_SIZE; z++)
                {
                    for (int y = 0; y < ChunkData.CHUNK_SIZE; y++)
                    {
                        // Convert chunk-local coordinates to world coordinates
                        int worldX = (chunkPos.x * ChunkData.CHUNK_SIZE) + x;
                        int worldY = (chunkPos.y * ChunkData.CHUNK_SIZE) + y;
                        int worldZ = (chunkPos.z * ChunkData.CHUNK_SIZE) + z;

                        byte blockType = worldGenerator.GetBlockAt(worldX, worldY, worldZ);
                        if (blockType != BlockType.Air.ID)
                        {
                            chunk.SetBlock(new Vector3Int(x, y, z), blockType);
                        }
                    }
                }
            }

            chunkManager.StoreChunkData(chunkPos, chunk);
        }

        visualizationManager.CreateChunkMesh(chunk);
    }

    /// <summary>
    /// Get block at world position
    /// </summary>
    public byte GetBlockAt(Vector3Int worldPos)
    {
        Vector3Int chunkPos = WorldToChunkPos(worldPos);
        Vector3Int localPos = WorldToLocalPos(worldPos);
        
        ChunkData chunk = chunkManager.GetChunkData(chunkPos);
        return chunk?.GetBlock(localPos) ?? BlockType.Air.ID;
    }

    /// <summary>
    /// Set block at world position
    /// </summary>
    public void SetBlockAt(Vector3Int worldPos, byte blockType)
    {
        Vector3Int chunkPos = WorldToChunkPos(worldPos);
        Vector3Int localPos = WorldToLocalPos(worldPos);
        
        ChunkData chunk = chunkManager.GetChunkData(chunkPos);
        if (chunk != null)
        {
            chunk.SetBlock(localPos, blockType);
            visualizationManager.UpdateChunkMesh(chunk);
        }
    }

    // Utility methods
    private Vector3Int WorldToChunkPos(Vector3Int worldPos)
    {
        return new Vector3Int(
            Mathf.FloorToInt((float)worldPos.x / ChunkData.CHUNK_SIZE),
            Mathf.FloorToInt((float)worldPos.y / ChunkData.CHUNK_SIZE),
            Mathf.FloorToInt((float)worldPos.z / ChunkData.CHUNK_SIZE)
        );
    }

    private Vector3Int WorldToLocalPos(Vector3Int worldPos)
    {
        return new Vector3Int(
            Mod(worldPos.x, ChunkData.CHUNK_SIZE),
            worldPos.y,
            Mod(worldPos.z, ChunkData.CHUNK_SIZE)
        );
    }

    private int Mod(int x, int m)
    {
        int r = x % m;
        return r < 0 ? r + m : r;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            visualizationManager?.Cleanup();
            chunkManager?.Cleanup();
            worldGenerator?.Cleanup();
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(debugBoxCenter, debugBoxSize);
    }

    private void StopCoroutineForChunk(Vector3Int chunkPos)
    {
        if (activeCoroutines.TryGetValue(chunkPos, out Coroutine coroutine))
        {
            if (coroutine != null)
            {
                StopCoroutine(coroutine);
            }
            activeCoroutines.Remove(chunkPos);
        }
    }

    private IEnumerator LoadChunkAsync(Vector3Int chunkPos)
    {
        chunksInProgress.Add(chunkPos);
        
        // Store the coroutine reference
        activeCoroutines[chunkPos] = StartCoroutine(LoadChunkAsyncInternal(chunkPos));
        yield return activeCoroutines[chunkPos];
        
        // Cleanup the reference
        if (activeCoroutines.ContainsKey(chunkPos))
        {
            activeCoroutines.Remove(chunkPos);
        }
    }

    private IEnumerator LoadChunkAsyncInternal(Vector3Int chunkPos)
    {
        // Early exit if chunk is no longer needed
        if (!activeChunks.Contains(chunkPos))
        {
            chunksInProgress.Remove(chunkPos);
            yield break;
        }

        // Create chunk data
        ChunkData chunk = new ChunkData(chunkPos);
        
        // Generate terrain on a background thread
        yield return new WaitForThreadedTask(() => {
            worldGenerator.GenerateChunk(chunk);
        });

        // Check again if chunk is still needed after generation
        if (!activeChunks.Contains(chunkPos))
        {
            chunksInProgress.Remove(chunkPos);
            yield break;
        }

        // Store chunk data and create visualization
        chunkManager.StoreChunkData(chunkPos, chunk);
        visualizationManager.CreateChunkMesh(chunk);

        chunksInProgress.Remove(chunkPos);
    }
}

// Helper class for sorting chunk requests
public struct ChunkRequest
{
    public Vector3Int Position;
    public float Distance;

    public ChunkRequest(Vector3Int pos, float dist)
    {
        Position = pos;
        Distance = dist;
    }
} 