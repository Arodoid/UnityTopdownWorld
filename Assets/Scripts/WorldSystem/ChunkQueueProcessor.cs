using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using VoxelGame.WorldSystem.Generation.Core;  // For WorldGenerator
using System.Linq;
using VoxelGame.Utilities;


public class ChunkQueueProcessor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera mainCamera;
    
    [Header("Performance Settings")]
    [SerializeField] private int maxChunksPerFrame = 30;
    [SerializeField] private int maxPendingChunks = 5000;
    [SerializeField] private float boundsPadding = 1f; // Chunks beyond visible area
    [SerializeField] private int batchSize = 8; // Adjust this value as needed
    [SerializeField] private int maxColumnsPerFrame = 8; // Add this field

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

    // New Y-level tracking
    private int currentYLimit = int.MaxValue;
    private HashSet<Vector3Int> chunksAffectedByYLimit = new HashSet<Vector3Int>();
    private Dictionary<Vector3Int, int> chunkYLevels = new Dictionary<Vector3Int, int>();

    // Add these fields at the class level
    private HashSet<Vector3Int> markedForRemoval = new HashSet<Vector3Int>();
    private bool needsQueueCleanup = false;

    // Add to existing fields
    private ObjectPool<Chunk> chunkPool;
    private ObjectPool<Mesh> meshPool;

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
        
        InitializePools();
        chunkRenderer = new ChunkRenderer(meshPool, this);
        
        if (mainCamera == null)
            mainCamera = Camera.main;

        minYLevel = 0;
        maxYLevel = WorldDataManager.WORLD_HEIGHT_IN_CHUNKS - 1;
    }

    private void InitializePools()
    {
        chunkPool = new ObjectPool<Chunk>(
            createFunc: () => new Chunk(Vector3Int.zero),
            onGet: chunk => 
            {
                chunk.Initialize();
                chunk.ClearBlocks();
            },
            onRelease: chunk => chunk.ClearBlocks(),
            initialSize: 100,
            maxSize: 1000
        );

        meshPool = new ObjectPool<Mesh>(
            createFunc: () => new Mesh(),
            onGet: mesh => 
            {
                mesh.Clear();
                mesh.MarkDynamic();
            },
            onRelease: mesh => mesh.Clear(),
            initialSize: 100,
            maxSize: 1000
        );
    }

    private void Update()
    {
        CleanQueues();
        ProcessGenerationQueue();
        ProcessRenderQueue();
        // chunkManager.UnloadMarkedChunks();
    }

    private async void ProcessGenerationQueue()
    {
        if (generationQueue.Count == 0) return;

        float startTime = Time.realtimeSinceStartup;
        
        // Group chunks by column
        Dictionary<Vector2Int, List<Vector3Int>> columnGroups = new();
        int processedColumns = 0;

        while (generationQueue.Count > 0 && processedColumns < maxColumnsPerFrame)
        {
            // Dequeue first, then use the position
            Vector3Int pos = generationQueue.Dequeue();
            Vector2Int column = new Vector2Int(pos.x, pos.z);

            if (!columnGroups.ContainsKey(column))
            {
                columnGroups[column] = new List<Vector3Int>();
                processedColumns++;
            }
            
            columnGroups[column].Add(pos);
            queuedForGeneration.Remove(pos);
        }

        // Process each column in parallel
        var columnTasks = columnGroups.Select(kvp => 
            ProcessColumnAsync(kvp.Key.x, kvp.Key.y, lastCenter, new List<(Vector3Int, int)>())
        ).ToList();

        // Wait for all columns to complete
        await Task.WhenAll(columnTasks);

        LogPerformanceStats();
    }

    private void ProcessRenderQueue()
    {
        if (!BlockRegistry.IsInitialized)
        {
            Debug.LogWarning("Waiting for BlockRegistry initialization...");
            return;
        }

        int chunksProcessed = 0;

        while (renderQueue.Count > 0 && chunksProcessed < maxChunksPerFrame)
        {
            float meshStart = Time.realtimeSinceStartup;
            
            Chunk chunk = renderQueue.Dequeue();
            queuedForRender.Remove(chunk.Position);

            int yLimit = currentYLimit;
            if (chunkYLevels.TryGetValue(chunk.Position, out int storedYLimit))
            {
                yLimit = storedYLimit;
            }

            // Get mesh from pool
            Mesh mesh = meshPool.Get();
            meshGenerator.GenerateMeshPooled(chunk, mesh, yLimit);
            meshGenTime += Time.realtimeSinceStartup - meshStart;

            float renderStart = Time.realtimeSinceStartup;
            chunkRenderer.RenderChunkPooled(chunk, mesh, meshPool);
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

        // Create a list to store all column tasks
        var columnTasks = new List<Task>();
        var columnResults = new List<List<(Vector3Int pos, int priority)>>();

        // Process columns
        for (int x = minChunkX; x <= maxChunkX; x++)
        for (int z = minChunkZ; z <= maxChunkZ; z++)
        {
            Vector2Int column = new Vector2Int(x, z);
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
                var columnChunks = new List<(Vector3Int pos, int priority)>();
                columnResults.Add(columnChunks);
                
                // Start the column processing without awaiting
                columnTasks.Add(ProcessColumnAsync(x, z, center, columnChunks));
                
                // Limit parallel columns if needed
                if (columnTasks.Count >= maxColumnsPerFrame)
                {
                    await Task.WhenAll(columnTasks);
                    columnTasks.Clear();
                }
            }
        }

        // Wait for any remaining columns
        if (columnTasks.Count > 0)
        {
            await Task.WhenAll(columnTasks);
        }

        // Combine all results
        foreach (var columnResult in columnResults)
        {
            chunks.AddRange(columnResult);
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

        // Create a local list for thread safety
        var columnChunks = new List<(Vector3Int pos, int priority)>();

        // Process from top to bottom, but one at a time to respect opacity
        for (int y = effectiveMaxY; y >= minYLevel; y--)
        {
            Vector3Int pos = new Vector3Int(x, y, z);
            
            // Skip if already being processed
            bool isQueued;
            lock (lockObject)
            {
                isQueued = queuedForGeneration.Contains(pos);
            }
            if (isQueued) continue;

            // Skip if entirely above Y limit
            int chunkBottomY = y * Chunk.ChunkSize;
            if (chunkBottomY > currentYLimit)
                continue;

            // Generate this chunk
            Chunk chunk = await worldGenerator.GenerateChunkPooled(pos, chunkPool);
            
            if (chunk != null)
            {
                lock (lockObject)
                {
                    chunkManager.StoreChunk(pos, chunk);
                    
                    if (!chunk.IsFullyEmpty())
                    {
                        int priority = CalculateChunkPriority(pos, center);
                        columnChunks.Add((pos, priority));
                        QueueChunkRender(chunk);
                        chunkYLevels[pos] = currentYLimit;
                    }
                }

                // Stop processing this column if we hit an opaque chunk
                if (chunk.IsOpaque)
                    break;
            }
        }

        // Add all chunks from this column at once
        lock (lockObject)
        {
            chunks.AddRange(columnChunks);
        }
    }

    private void RemoveChunk(Vector3Int position)
    {
        // Mark the position for removal and set the cleanup flag
        markedForRemoval.Add(position);
        needsQueueCleanup = true;
        
        // Immediate removals that don't require queue rebuilding
        queuedForGeneration.Remove(position);
        queuedForRender.Remove(position);
        chunkRenderer.RemoveChunkRender(position);
    }

    // Add this method to clean queues efficiently
    private void CleanQueues()
    {
        if (!needsQueueCleanup || markedForRemoval.Count == 0) return;

        // Clean generation queue
        var genQueue = new List<Vector3Int>();
        while (generationQueue.Count > 0)
        {
            genQueue.Add(generationQueue.Dequeue());
        }
        
        foreach (var item in genQueue)
        {
            if (!markedForRemoval.Contains(item))
            {
                generationQueue.Enqueue(item, CalculateChunkPriority(item, lastCenter));
            }
        }

        // Clean render queue
        var renderList = new List<Chunk>();
        while (renderQueue.Count > 0)
        {
            var chunk = renderQueue.Dequeue();
            if (!markedForRemoval.Contains(chunk.Position))
            {
                renderList.Add(chunk);
            }
        }
        
        foreach (var chunk in renderList)
        {
            renderQueue.Enqueue(chunk);
        }

        markedForRemoval.Clear();
        needsQueueCleanup = false;
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

    private void OnDestroy()
    {
        // Clean up pools with null checks
        if (meshPool != null)
            meshPool.Clear();
        
        if (chunkPool != null)
            chunkPool.Clear();
    }
}