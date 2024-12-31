using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using VoxelGame.WorldSystem.Generation.Core;  // For WorldGenerator

public class ChunkQueueProcessor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera mainCamera;
    
    [Header("Performance Settings")]
    [SerializeField] private int maxChunksPerFrame = 30;
    [SerializeField] private int maxPendingChunks = 5000;
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField] private float boundsPadding = 1f; // Chunks beyond visible area

    // World height limits
    private int minYLevel;
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

    // New Y-level tracking
    private int currentYLimit = int.MaxValue;
    private HashSet<Vector3Int> chunksAffectedByYLimit = new HashSet<Vector3Int>();
    private Dictionary<Vector3Int, int> chunkYLevels = new Dictionary<Vector3Int, int>();

    // Public Properties
    public int GenerationQueueCount => generationQueue.Count;
    public int RenderQueueCount => renderQueue.Count;
    public int QueuedForGenerationCount => queuedForGeneration.Count;
    public int QueuedForRenderCount => queuedForRender.Count;
    public ICollection<Vector3Int> CurrentlyVisibleChunks => currentlyVisibleChunks;

    private void Awake()
    {
        worldGenerator = GetComponent<WorldGenerator>();
        chunkManager = GetComponent<ChunkManager>();
        meshGenerator = new ChunkMeshGenerator();
        chunkRenderer = new ChunkRenderer();
        
        if (mainCamera == null)
            mainCamera = Camera.main;

        minYLevel = 0;
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

    private async void ProcessGenerationQueue()
    {
        if (generationQueue.Count == 0) return;

        float startTime = Time.realtimeSinceStartup;
        int chunksProcessed = 0;
        
        while (generationQueue.Count > 0 && chunksProcessed < maxChunksPerFrame)
        {
            float genStart = Time.realtimeSinceStartup;
            
            Vector3Int pos = generationQueue.Dequeue();
            queuedForGeneration.Remove(pos);

            // Skip if chunk already exists
            if (chunkManager.ChunkExists(pos))
                continue;

            // Generate the chunk
            float beforeGen = Time.realtimeSinceStartup;
            Chunk chunk = await worldGenerator.GenerateChunk(pos);
            generationTime += Time.realtimeSinceStartup - beforeGen;

            if (chunk != null)
            {
                chunkManager.StoreChunk(pos, chunk);
                
                if (!chunk.IsFullyEmpty())
                {
                    QueueChunkRender(chunk);
                }
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

            // Get the Y-level for this chunk
            int yLimit = currentYLimit;
            if (chunkYLevels.TryGetValue(chunk.Position, out int storedYLimit))
            {
                yLimit = storedYLimit;
            }

            // Generate mesh with Y-level limit
            Mesh mesh = meshGenerator.GenerateMesh(chunk, yLimit);
            meshGenTime += Time.realtimeSinceStartup - meshStart;

            float renderStart = Time.realtimeSinceStartup;
            chunkRenderer.RenderChunk(chunk, mesh);
            renderTime += Time.realtimeSinceStartup - renderStart;
            
            chunk.ClearDirty();
            chunksProcessed++;
        }
    }

    public async void ScanChunksAsync(CameraVisibilityController.ViewData viewData)
    {
        if (isScanning) return;

        bool yLevelChanged = currentYLimit != viewData.YLevel;
        currentYLimit = viewData.YLevel;
        
        // Add padding to bounds to prevent pop-in
        Bounds paddedBounds = new Bounds(
            viewData.ViewBounds.center,
            viewData.ViewBounds.size * (1 + boundsPadding)
        );

        if (viewData.CenterChunk == lastCenter && 
            paddedBounds.Equals(lastViewBounds) && 
            !yLevelChanged) 
            return;

        isScanning = true;
        lastCenter = viewData.CenterChunk;
        lastViewBounds = paddedBounds;

        // Handle Y-level changes for existing chunks
        if (yLevelChanged)
        {
            HandleYLevelChange(viewData.YLevel);
        }

        // Clear chunks outside view (existing logic)
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
                chunkYLevels.Remove(pos); // Clean up Y-level tracking
            }
        }

        // Calculate new chunks to add
        List<(Vector3Int pos, int priority)> chunksToAdd = 
            await CalculateVisibleChunksAsync(viewData.ViewBounds, viewData.CenterChunk);

        // Queue new chunks
        lock (lockObject)
        {
            foreach (var (pos, priority) in chunksToAdd)
            {
                if (!currentlyVisibleChunks.Contains(pos))
                {
                    if (chunkManager.ChunkExists(pos))
                    {
                        Chunk chunk = chunkManager.GetChunk(pos);
                        if (!chunk.IsFullyEmpty())  // Only queue if not empty
                        {
                            QueueChunkRender(chunk);
                        }
                        currentlyVisibleChunks.Add(pos);
                        chunkYLevels[pos] = currentYLimit;
                    }
                    else if (!queuedForGeneration.Contains(pos) && 
                             generationQueue.Count < maxPendingChunks)
                    {
                        generationQueue.Enqueue(pos, priority);
                        queuedForGeneration.Add(pos);
                        currentlyVisibleChunks.Add(pos);
                        chunkYLevels[pos] = currentYLimit;
                    }
                }
            }
        }

        isScanning = false;
    }

    private void HandleYLevelChange(int newYLimit)
    {
        chunksAffectedByYLimit.Clear();

        // Identify chunks that need remeshing
        foreach (var pos in currentlyVisibleChunks)
        {
            if (chunkYLevels.TryGetValue(pos, out int currentChunkYLevel))
            {
                // Skip if chunk exists and is fully empty
                Chunk chunk = chunkManager.GetChunk(pos);
                if (chunk != null && chunk.IsFullyEmpty())
                    continue;

                int chunkYOffset = pos.y * Chunk.ChunkSize;
                int oldEffectiveY = currentChunkYLevel - chunkYOffset;
                int newEffectiveY = newYLimit - chunkYOffset;

                // Only requeue if this chunk contains either the old or new Y-level cut
                bool containsOldCut = oldEffectiveY >= 0 && oldEffectiveY < Chunk.ChunkSize;
                bool containsNewCut = newEffectiveY >= 0 && newEffectiveY < Chunk.ChunkSize;

                if (containsOldCut || containsNewCut)
                {
                    chunksAffectedByYLimit.Add(pos);
                    chunkYLevels[pos] = newYLimit;
                }
            }
        }

        // Queue affected chunks for remeshing
        foreach (var pos in chunksAffectedByYLimit)
        {
            if (chunkManager.ChunkExists(pos))
            {
                Chunk chunk = chunkManager.GetChunk(pos);
                if (!chunk.IsFullyEmpty())  // Double check in case chunk was modified
                {
                    QueueChunkRender(chunk);
                }
            }
        }
    }

    private async Task<List<(Vector3Int pos, int priority)>> CalculateVisibleChunksAsync(
        Bounds viewBounds, Vector3Int center)
    {
        List<(Vector3Int pos, int priority)> chunks = new List<(Vector3Int pos, int priority)>();
        
        // Calculate chunk bounds from view bounds
        int minChunkX = Mathf.FloorToInt(viewBounds.min.x / Chunk.ChunkSize);
        int maxChunkX = Mathf.CeilToInt(viewBounds.max.x / Chunk.ChunkSize);
        int minChunkZ = Mathf.FloorToInt(viewBounds.min.z / Chunk.ChunkSize);
        int maxChunkZ = Mathf.CeilToInt(viewBounds.max.z / Chunk.ChunkSize);

        // Track which columns we've processed
        HashSet<Vector2Int> processedColumns = new HashSet<Vector2Int>();

        // Process columns
        for (int x = minChunkX; x <= maxChunkX; x++)
        for (int z = minChunkZ; z <= maxChunkZ; z++)
        {
            Vector2Int column = new Vector2Int(x, z);
            if (processedColumns.Contains(column))
                continue;

            Vector3 chunkWorldPos = new Vector3(
                x * Chunk.ChunkSize,
                0,
                z * Chunk.ChunkSize
            );

            Bounds chunkBounds = new Bounds(
                chunkWorldPos + Vector3.one * (Chunk.ChunkSize / 2f),
                Vector3.one * Chunk.ChunkSize
            );

            if (chunkBounds.Intersects(viewBounds))
            {
                await ProcessColumnAsync(x, z, center, chunks);
                processedColumns.Add(column);
            }
        }

        return chunks;
    }

    private async Task ProcessColumnAsync(int x, int z, Vector3Int center, List<(Vector3Int pos, int priority)> chunks)
    {
        // Calculate the maximum Y level we should process based on currentYLimit
        int effectiveMaxY = Mathf.Min(
            maxYLevel, 
            Mathf.CeilToInt((float)currentYLimit / Chunk.ChunkSize)
        );

        // Process from effective max Y down to minYLevel
        for (int y = effectiveMaxY; y >= minYLevel; y--)
        {
            Vector3Int pos = new Vector3Int(x, y, z);
            
            // Skip if this chunk is already being processed
            if (queuedForGeneration.Contains(pos))
                continue;

            // Skip if this chunk is entirely above the Y limit
            int chunkBottomY = y * Chunk.ChunkSize;
            if (chunkBottomY > currentYLimit)
                continue;

            // Get or generate the chunk
            Chunk chunk = chunkManager.GetChunk(pos);
            if (chunk == null)
            {
                int priority = CalculateChunkPriority(pos, center);
                chunks.Add((pos, priority));

                chunk = await worldGenerator.GenerateChunk(pos);
                if (chunk != null)
                {
                    chunkManager.StoreChunk(pos, chunk);
                    
                    if (!chunk.IsFullyEmpty())
                    {
                        lock (lockObject)
                        {
                            QueueChunkRender(chunk);
                            chunkYLevels[pos] = currentYLimit; // Store Y-level for rendering
                        }
                    }
                }
            }
            else if (!chunk.IsFullyEmpty())
            {
                int priority = CalculateChunkPriority(pos, center);
                chunks.Add((pos, priority));
                chunkYLevels[pos] = currentYLimit; // Update Y-level for existing chunks
            }

            // Stop processing this column if we hit an opaque chunk
            if (chunk != null && chunk.IsOpaque)
            {
                break;
            }
        }
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

        // Convert chunk position to world space
        Vector3 chunkWorldPos = new Vector3(
            position.x * Chunk.ChunkSize,
            position.y * Chunk.ChunkSize,
            position.z * Chunk.ChunkSize
        );

        // Create chunk bounds
        Bounds chunkBounds = new Bounds(
            chunkWorldPos + Vector3.one * (Chunk.ChunkSize / 2f),
            Vector3.one * Chunk.ChunkSize
        );

        // Check if within Y range and view bounds
        bool withinY = position.y >= minYLevel && position.y <= maxYLevel;
        return withinY && chunkBounds.Intersects(lastViewBounds);
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