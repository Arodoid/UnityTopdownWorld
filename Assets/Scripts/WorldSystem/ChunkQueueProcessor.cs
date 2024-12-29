using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;

public class ChunkQueueProcessor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera mainCamera;
    
    [Header("Performance Settings")]
    [SerializeField] private int maxChunksPerFrame = 30;
    [SerializeField] private int maxPendingChunks = 5000;
    [SerializeField] private float viewDistance = 128f;
    [SerializeField] private bool showDebugInfo = true;
    
    private int minYLevel = 0;
    private int maxYLevel;

    private WorldGenerator worldGenerator;
    private ChunkManager chunkManager;
    private ChunkRenderer chunkRenderer;
    private ChunkMeshGenerator meshGenerator;
    
    // Queue Management
    private PriorityQueue<Vector3Int> generationQueue = new PriorityQueue<Vector3Int>();    
    private Queue<Chunk> renderQueue = new Queue<Chunk>();
    private HashSet<Vector3Int> queuedForGeneration = new HashSet<Vector3Int>();
    private HashSet<Vector3Int> queuedForRender = new HashSet<Vector3Int>();
    private HashSet<Vector3Int> currentlyVisibleChunks = new HashSet<Vector3Int>();

    // View State
    private Vector3Int lastCenter;
    private Bounds lastViewBounds;
    private bool isScanning = false;
    private object lockObject = new object();

    // Performance Monitoring
    private float generationTime = 0f;
    private float meshGenTime = 0f;
    private float renderTime = 0f;
    private int operationsCount = 0;
    private float nextLogTime = 0f;
    private const float LOG_INTERVAL = 2f;
    private GUIStyle debugTextStyle;

    // Public Properties
    public int GenerationQueueCount => generationQueue.Count;
    public int RenderQueueCount => renderQueue.Count;
    public int QueuedForGenerationCount => queuedForGeneration.Count;
    public int QueuedForRenderCount => queuedForRender.Count;
    public float ViewDistance => viewDistance;

    private void Awake()
    {
        worldGenerator = GetComponent<WorldGenerator>();
        chunkManager = GetComponent<ChunkManager>();
        meshGenerator = new ChunkMeshGenerator();
        chunkRenderer = new ChunkRenderer();
        
        if (mainCamera == null)
            mainCamera = Camera.main;

        // Set Y levels based on world height
        minYLevel = 0;  // Usually start at ground level
        maxYLevel = WorldDataManager.WORLD_HEIGHT_IN_CHUNKS - 1;

        debugTextStyle = new GUIStyle
        {
            normal = { textColor = Color.white },
            fontSize = 14,
            padding = new RectOffset(10, 10, 10, 10)
        };
    }

    private void Update()
    {
        ProcessGenerationQueue();
        ProcessRenderQueue();
        // chunkManager.UnloadMarkedChunks();
    }

    private void ProcessGenerationQueue()
    {
        float startTime = Time.realtimeSinceStartup;
        int chunksProcessed = 0;
        
        while (generationQueue.Count > 0 && chunksProcessed < maxChunksPerFrame)
        {
            float genStart = Time.realtimeSinceStartup;
            
            Vector3Int pos = generationQueue.Dequeue();
            queuedForGeneration.Remove(pos);

            if (chunkManager.ChunkExists(pos))
                continue;

            Chunk chunk = worldGenerator.GenerateChunk(pos);
            chunkManager.StoreChunk(pos, chunk);

            generationTime += Time.realtimeSinceStartup - genStart;
            
            if (!chunk.IsFullyEmpty())
            {
                QueueChunkRender(chunk);
            }

            chunksProcessed++;
            operationsCount++;
        }

        LogPerformanceStats();
    }

    private void ProcessRenderQueue()
    {
        int chunksProcessed = 0;

        while (renderQueue.Count > 0 && chunksProcessed < maxChunksPerFrame)
        {
            float meshStart = Time.realtimeSinceStartup;
            
            Chunk chunk = renderQueue.Dequeue();
            queuedForRender.Remove(chunk.Position);

            Mesh mesh = meshGenerator.GenerateMesh(chunk);
            meshGenTime += Time.realtimeSinceStartup - meshStart;

            float renderStart = Time.realtimeSinceStartup;
            chunkRenderer.RenderChunk(chunk, mesh);
            renderTime += Time.realtimeSinceStartup - renderStart;
            
            chunk.ClearDirty();
            chunksProcessed++;
        }
    }

    public async void ScanChunksAsync(Vector3Int center)
    {
        if (isScanning) return;
        
        Bounds viewBounds = CalculateViewBounds();
        if (center == lastCenter && viewBounds.Equals(lastViewBounds)) 
            return;

        isScanning = true;
        lastCenter = center;
        lastViewBounds = viewBounds;

        // Clear chunks outside view
        lock (lockObject)
        {
            List<Vector3Int> positionsToRemove = new List<Vector3Int>();
            foreach (Vector3Int pos in currentlyVisibleChunks)
            {
                if (!IsChunkInView(pos))
                {
                    positionsToRemove.Add(pos);
                }
            }

            foreach (Vector3Int pos in positionsToRemove)
            {
                RemoveChunk(pos);
                currentlyVisibleChunks.Remove(pos);
            }
        }

        // Calculate new chunks to add
        List<(Vector3Int pos, int priority)> chunksToAdd = await CalculateVisibleChunksAsync(viewBounds, center);

        // Queue new chunks
        lock (lockObject)
        {
            foreach (var (pos, priority) in chunksToAdd)
            {
                if (!currentlyVisibleChunks.Contains(pos))  // If not currently visible
                {
                    if (chunkManager.ChunkExists(pos))      // If data exists
                    {
                        // Re-queue for rendering
                        QueueChunkRender(chunkManager.GetChunk(pos));
                        currentlyVisibleChunks.Add(pos);
                    }
                    else if (!queuedForGeneration.Contains(pos) && 
                             generationQueue.Count < maxPendingChunks)
                    {
                        // New chunk needs generation
                        generationQueue.Enqueue(pos, priority);
                        queuedForGeneration.Add(pos);
                        currentlyVisibleChunks.Add(pos);
                    }
                }
            }
        }

        isScanning = false;
    }

    private Bounds CalculateViewBounds()
    {
        if (mainCamera == null) return new Bounds();

        float height = 2f * mainCamera.orthographicSize;
        float width = height * mainCamera.aspect;
        
        Vector3 center = mainCamera.transform.position;
        // Now we only care about X/Z for bounds, Y is handled separately
        Vector3 size = new Vector3(width + viewDistance, 1000f, height + viewDistance);
        
        return new Bounds(center, size);
    }

    private async Task<List<(Vector3Int pos, int priority)>> CalculateVisibleChunksAsync(Bounds viewBounds, Vector3Int center)
    {
        List<(Vector3Int pos, int priority)> chunks = new List<(Vector3Int pos, int priority)>();
        
        await Task.Run(() =>
        {
            // Calculate X/Z bounds from camera view
            int minChunkX = Mathf.FloorToInt((viewBounds.min.x) / Chunk.ChunkSize);
            int maxChunkX = Mathf.CeilToInt((viewBounds.max.x) / Chunk.ChunkSize);
            int minChunkZ = Mathf.FloorToInt((viewBounds.min.z) / Chunk.ChunkSize);
            int maxChunkZ = Mathf.CeilToInt((viewBounds.max.z) / Chunk.ChunkSize);

            // For each X/Z position in view
            for (int x = minChunkX; x <= maxChunkX; x++)
            {
                for (int z = minChunkZ; z <= maxChunkZ; z++)
                {
                    // Calculate XZ distance for view distance check
                    Vector2 chunkXZ = new Vector2(x * Chunk.ChunkSize, z * Chunk.ChunkSize);
                    Vector2 centerXZ = new Vector2(center.x * Chunk.ChunkSize, center.z * Chunk.ChunkSize);
                    
                    if (Vector2.Distance(chunkXZ, centerXZ) <= viewDistance)
                    {
                        // Add ALL Y levels within our range
                        for (int y = minYLevel; y <= maxYLevel; y++)
                        {
                            Vector3Int pos = new Vector3Int(x, y, z);
                            // Still use 3D distance for priority
                            int priority = CalculateChunkPriority(pos, center);
                            chunks.Add((pos, priority));
                        }
                    }
                }
            }
        });

        return chunks;
    }

    private void RemoveChunk(Vector3Int position)
    {
        queuedForGeneration.Remove(position);
        queuedForRender.Remove(position);
        chunkRenderer.RemoveChunkRender(position);
        
        // Clean up generation queue
        var tempGenerationQueue = new PriorityQueue<Vector3Int>();
        while (generationQueue.Count > 0)
        {
            var pos = generationQueue.Dequeue();
            if (pos != position)
            {
                tempGenerationQueue.Enqueue(pos, CalculateChunkPriority(pos, lastCenter));
            }
        }
        generationQueue = tempGenerationQueue;

        // Clean up render queue
        var tempRenderQueue = new Queue<Chunk>();
        while (renderQueue.Count > 0)
        {
            var chunk = renderQueue.Dequeue();
            if (chunk.Position != position)
            {
                tempRenderQueue.Enqueue(chunk);
            }
        }
        renderQueue = tempRenderQueue;
    }

    private void QueueChunkRender(Chunk chunk)
    {
        if (!queuedForRender.Contains(chunk.Position))
        {
            renderQueue.Enqueue(chunk);
            queuedForRender.Add(chunk.Position);
        }
    }

    private int CalculateChunkPriority(Vector3Int chunkPos, Vector3Int centerPos)
    {
        return Mathf.RoundToInt(Vector3Int.Distance(chunkPos, centerPos));
    }

    public bool IsChunkInView(Vector3Int position)
    {
        if (lastViewBounds == null) return false;

        // Convert to world position
        Vector2 chunkXZ = new Vector2(
            position.x * Chunk.ChunkSize,
            position.z * Chunk.ChunkSize
        );
        
        Vector2 centerXZ = new Vector2(
            lastCenter.x * Chunk.ChunkSize,
            lastCenter.z * Chunk.ChunkSize
        );

        // Check if within view distance on XZ plane
        bool withinXZ = Vector2.Distance(chunkXZ, centerXZ) <= viewDistance;
        // Check if within Y range
        bool withinY = position.y >= minYLevel && position.y <= maxYLevel;

        return withinXZ && withinY;
    }

    private void LogPerformanceStats()
    {
        if (Time.time >= nextLogTime)
        {
            if (operationsCount > 0)
            {
                Debug.Log($"Average Times (ms):\n" +
                         $"Generation: {(generationTime / operationsCount) * 1000:F2}\n" +
                         $"Mesh Gen: {(meshGenTime / operationsCount) * 1000:F2}\n" +
                         $"Rendering: {(renderTime / operationsCount) * 1000:F2}");
            }
            
            generationTime = 0f;
            meshGenTime = 0f;
            renderTime = 0f;
            operationsCount = 0;
            nextLogTime = Time.time + LOG_INTERVAL;
        }
    }

    private void OnGUI()
    {
        if (!showDebugInfo) return;

        GUI.Label(new Rect(Screen.width - 300, 10, 300, 160),
            $"=== Chunk Processing ===\n" +
            $"Generation Queue: {generationQueue.Count}\n" +
            $"Render Queue: {renderQueue.Count}\n" +
            $"Queued for Generation: {queuedForGeneration.Count}\n" +
            $"Queued for Render: {queuedForRender.Count}\n" +
            $"Currently Visible: {currentlyVisibleChunks.Count}",
            debugTextStyle);
    }
}