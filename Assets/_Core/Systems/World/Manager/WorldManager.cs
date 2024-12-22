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

    private string lastViewKey; // Add this field to track view changes

    // Add a way to track chunks that are "in progress"
    private HashSet<Vector3Int> chunksInQueue = new HashSet<Vector3Int>();

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
        
        while (chunkLoadQueue.Count > 0 && chunksThisFrame < chunksPerFrame)
        {
            Vector3Int chunkPos = chunkLoadQueue.Peek();
            
            // Skip if chunk is no longer needed
            if (!activeChunks.Contains(chunkPos))
            {
                chunkLoadQueue.Dequeue();
                chunksInQueue.Remove(chunkPos);
                continue;
            }
            
            if (!chunksInProgress.Contains(chunkPos))
            {
                chunkLoadQueue.Dequeue();
                StartCoroutine(LoadChunkAsync(chunkPos));
                chunksThisFrame++;
            }
        }

        // Cleanup any chunks in chunksInQueue that aren't in activeChunks
        chunksInQueue.RemoveWhere(pos => !activeChunks.Contains(pos));
    }

    // Public API for external systems

    /// <summary>
    /// Request chunk generation and visualization for an area, cleaning up old chunks
    /// </summary>
    public void RequestChunkArea(Vector2Int center, int radius, Vector3 cameraPos)
    {
        Debug.Log($"[WorldManager] Request: Center({center}), Radius({radius}), Camera({cameraPos})");
        Debug.Log($"[WorldManager] View Size: {radius * 2}x{radius * 2} chunks ({radius * 2 * ChunkData.CHUNK_SIZE}x{radius * 2 * ChunkData.CHUNK_SIZE} blocks)");
        
        // Update debug visualization data
        float width = radius * 2 * ChunkData.CHUNK_SIZE;
        float height = ChunkData.WORLD_HEIGHT;
        float depth = radius * 2 * ChunkData.CHUNK_SIZE;
        
        debugBoxCenter = new Vector3(
            center.x * ChunkData.CHUNK_SIZE + (ChunkData.CHUNK_SIZE / 2f),
            height / 2f,
            center.y * ChunkData.CHUNK_SIZE + (ChunkData.CHUNK_SIZE / 2f)
        );
        
        debugBoxSize = new Vector3(width, height, depth);

        HashSet<Vector3Int> newActiveChunks = new HashSet<Vector3Int>();
        List<ChunkRequest> requests = new List<ChunkRequest>();

        for (int x = -radius; x <= radius; x++)
        {
            for (int z = -radius; z <= radius; z++)
            {
                bool foundOpaque = false;
                
                for (int y = ChunkData.WORLD_HEIGHT / ChunkData.CHUNK_SIZE - 1; y >= 0; y--)
                {
                    Vector3Int chunkPos = new Vector3Int(center.x + x, y, center.y + z);
                    ChunkData chunk = chunkManager.GetChunkData(chunkPos);
                    
                    if (!foundOpaque)
                    {
                        newActiveChunks.Add(chunkPos);
                        
                        // Only queue if chunk needs generation or visualization
                        if (chunk == null || !visualizationManager.IsChunkVisualized(chunkPos))
                        {
                            // Skip if we're already generating this chunk
                            if (!chunksInQueue.Contains(chunkPos))
                            {
                                float priority = GetChunkPriority(chunkPos, cameraPos);
                                requests.Add(new ChunkRequest(chunkPos, priority));
                                chunksInQueue.Add(chunkPos);
                            }
                        }
                    }

                    if (chunk != null && chunk.IsChunkOpaque())
                    {
                        foundOpaque = true;
                    }
                }
            }
        }

        // Queue only new or unvisualized chunks
        foreach (var request in requests)
        {
            chunkLoadQueue.Enqueue(request.Position);
        }

        // Cleanup chunks that are no longer active
        HashSet<Vector3Int> chunksToRemove = new HashSet<Vector3Int>(activeChunks);
        chunksToRemove.ExceptWith(newActiveChunks);
        foreach (var chunkPos in chunksToRemove)
        {
            CleanupChunk(chunkPos);
        }

        activeChunks = newActiveChunks;
    }

    private float GetChunkPriority(Vector3Int chunkPos, Vector3 cameraPos)
    {
        // Convert chunk position to world position (center of chunk)
        Vector3 chunkWorldPos = new Vector3(
            (chunkPos.x * ChunkData.CHUNK_SIZE) + (ChunkData.CHUNK_SIZE / 2f),
            (chunkPos.y * ChunkData.CHUNK_SIZE) + (ChunkData.CHUNK_SIZE / 2f),
            (chunkPos.z * ChunkData.CHUNK_SIZE) + (ChunkData.CHUNK_SIZE / 2f)
        );

        // Calculate distance from camera
        float distance = Vector3.Distance(chunkWorldPos, cameraPos);
        
        // Add height bias to prioritize higher chunks slightly
        float heightBias = (ChunkData.WORLD_HEIGHT - chunkPos.y) * 0.1f;
        
        return distance + heightBias;
    }

    private void CleanupChunk(Vector3Int chunkPos)
    {
        chunksInProgress.Remove(chunkPos);
        chunksInQueue.Remove(chunkPos);
        visualizationManager.RemoveChunkMesh(chunkPos);
        chunkManager.MarkChunkInactive(chunkPos);
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
        // Draw the request volume
        Gizmos.color = new Color(1f, 1f, 0f, 0.2f); // Yellow, very transparent
        Gizmos.DrawWireCube(debugBoxCenter, debugBoxSize);
        
        // Draw solid faces for the request volume
        Gizmos.color = new Color(1f, 1f, 0f, 0.05f); // Yellow, extremely transparent
        Gizmos.DrawCube(debugBoxCenter, debugBoxSize);

        // Draw active chunks
        if (!Application.isPlaying) return;
        
        foreach (Vector3Int chunkPos in activeChunks)
        {
            ChunkData chunk = chunkManager.GetChunkData(chunkPos);
            if (chunk == null) continue;

            Vector3 worldPos = new Vector3(
                chunkPos.x * ChunkData.CHUNK_SIZE,
                chunkPos.y * ChunkData.CHUNK_SIZE,
                chunkPos.z * ChunkData.CHUNK_SIZE
            );

            Vector3 chunkCenter = worldPos + Vector3.one * (ChunkData.CHUNK_SIZE / 2f);
            Vector3 chunkSize = Vector3.one * ChunkData.CHUNK_SIZE;

            // Draw chunks with different colors based on their state
            if (chunksInProgress.Contains(chunkPos))
            {
                // Loading chunks in yellow
                Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            }
            else if (!chunk.IsChunkOpaque())
            {
                // Transparent chunks in blue
                Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
            }
            else
            {
                // Opaque chunks in red
                Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            }
            
            Gizmos.DrawWireCube(chunkCenter, chunkSize);
        }
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
        try
        {
            // 1. Safety check - exit if chunk is no longer needed
            if (!activeChunks.Contains(chunkPos))
            {
                chunksInProgress.Remove(chunkPos);
                yield break;
            }

            // 2. Create new chunk and prepare for generation
            ChunkData chunk = new ChunkData(chunkPos);
            
            // 3. Generate chunk data on a separate thread
            yield return new WaitForThreadedTask(() => {
                worldGenerator.GenerateChunk(chunk);
            });

            // 4. Post-generation validation
            if (chunk.IsEmpty() || !activeChunks.Contains(chunkPos))
            {
                chunksInProgress.Remove(chunkPos);
                yield break;
            }

            // 5. Store the chunk data
            chunkManager.StoreChunkData(chunkPos, chunk);

            // 6. Occlusion check - see if this chunk should be visible
            bool shouldRender = true;
            for (int y = chunkPos.y + 1; y < ChunkData.WORLD_HEIGHT / ChunkData.CHUNK_SIZE; y++)
            {
                Vector3Int abovePos = new Vector3Int(chunkPos.x, y, chunkPos.z);
                ChunkData aboveChunk = chunkManager.GetChunkData(abovePos);
                if (aboveChunk != null && aboveChunk.IsChunkOpaque())
                {
                    shouldRender = false;
                    break;
                }
            }

            // 7. Create visual mesh if chunk should be visible
            if (shouldRender)
            {
                visualizationManager.CreateChunkMesh(chunk);
            }
        }
        finally
        {
            // Always clean up tracking collections
            chunksInProgress.Remove(chunkPos);
            chunksInQueue.Remove(chunkPos);
        }
    }

    public void UpdateWorldView(Vector3 viewerPos, float viewWidth, float viewHeight)
    {
        // Convert view dimensions to chunk space
        Vector2Int centerChunk = new Vector2Int(
            Mathf.FloorToInt(viewerPos.x / ChunkData.CHUNK_SIZE),
            Mathf.FloorToInt(viewerPos.z / ChunkData.CHUNK_SIZE)
        );

        int chunksNeededX = Mathf.CeilToInt(viewWidth / ChunkData.CHUNK_SIZE);
        int chunksNeededZ = Mathf.CeilToInt(viewHeight / ChunkData.CHUNK_SIZE);
        int radius = Mathf.Max(chunksNeededX, chunksNeededZ) / 2 + 1;

        // Only process if the view request is different from the last one
        string viewKey = $"{centerChunk}_{radius}_{viewerPos.y:F1}";
        if (viewKey == lastViewKey) return;
        lastViewKey = viewKey;

        Debug.Log($"[WorldManager] New view request: Center({centerChunk}), Radius({radius})");
        RequestChunkArea(centerChunk, radius, viewerPos);
    }

    // When chunk generation is complete:
    public void OnChunkGenerationComplete(Vector3Int chunkPos)
    {
        chunksInQueue.Remove(chunkPos);
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