using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using VoxelGame.Interfaces;

public class ChunkQueueProcessor : MonoBehaviour
{
    [SerializeField] private int maxChunksPerFrame = 8;

    private IChunkGenerator worldGenerator;
    private IChunkManager chunkManager;
    private IChunkMeshGenerator meshGenerator;
    private IChunkRenderer chunkRenderer;

    // Simple queues for processing
    private Queue<Vector2Int> columnGenerationQueue = new Queue<Vector2Int>();
    private Queue<Chunk> renderQueue = new Queue<Chunk>();
    private HashSet<Vector2Int> columnsInProgress = new HashSet<Vector2Int>();
    private HashSet<Vector2Int> completedColumns = new HashSet<Vector2Int>();
    
    private int currentYLimit = int.MaxValue;

    private List<Task> activeColumnTasks = new List<Task>();

    private void Awake()
    {
        worldGenerator = GetComponent<IChunkGenerator>();
        chunkManager = GetComponent<IChunkManager>();
        meshGenerator = GetComponent<IChunkMeshGenerator>();
        chunkRenderer = GetComponent<IChunkRenderer>();

        if (worldGenerator == null || chunkManager == null || 
            meshGenerator == null || chunkRenderer == null)
        {
            Debug.LogError("Missing required components on ChunkQueueProcessor");
        }
    }

    private void Update()
    {
        ProcessQueues();
    }

    public void UpdateVisibleChunks(CameraVisibilityController.ViewData viewData)
    {
        // Clear existing queues when view changes
        columnGenerationQueue.Clear();
        renderQueue.Clear();
        columnsInProgress.Clear();

        currentYLimit = viewData.YLevel;
        var columnsInView = GetColumnsInView(viewData.ViewBounds);
        var columnsToKeep = new HashSet<Vector2Int>(columnsInView);

        // Mark chunks for unloading if they're no longer in view
        foreach (var chunkPos in chunkManager.GetAllChunkPositions())
        {
            Vector2Int columnPos = new Vector2Int(chunkPos.x, chunkPos.z);
            if (!columnsToKeep.Contains(columnPos))
            {
                chunkManager.MarkForUnloading(chunkPos);
                chunkRenderer.RemoveChunkRender(chunkPos);
                completedColumns.Remove(columnPos); // Remove from completed when unloading
            }
        }

        // Process unloading
        chunkManager.UnloadMarkedChunks();

        // Queue new chunks for generation (skip completed columns)
        foreach (var columnPos in columnsInView)
        {
            if (!completedColumns.Contains(columnPos))
            {
                columnGenerationQueue.Enqueue(columnPos);
                columnsInProgress.Add(columnPos);
            }
        }
    }

    private bool ShouldRenderChunk(Chunk chunk)
    {
        return !chunk.IsFullyEmpty() && 
               (!chunkRenderer.IsChunkRendered(chunk.Position) || chunk.IsDirty());
    }

    private IEnumerable<Vector2Int> GetColumnsInView(Bounds viewBounds)
    {
        int minX = Mathf.FloorToInt(viewBounds.min.x / Chunk.ChunkSize);
        int maxX = Mathf.CeilToInt(viewBounds.max.x / Chunk.ChunkSize);
        int minZ = Mathf.FloorToInt(viewBounds.min.z / Chunk.ChunkSize);
        int maxZ = Mathf.CeilToInt(viewBounds.max.z / Chunk.ChunkSize);

        // Get center point in chunk coordinates
        Vector2Int center = new Vector2Int(
            Mathf.RoundToInt(viewBounds.center.x / Chunk.ChunkSize),
            Mathf.RoundToInt(viewBounds.center.z / Chunk.ChunkSize)
        );

        // Create list of all columns with their distances
        var columns = new List<(Vector2Int pos, float dist)>();
        
        for (int x = minX; x <= maxX; x++)
        for (int z = minZ; z <= maxZ; z++)
        {
            Vector2Int pos = new Vector2Int(x, z);
            float dist = Vector2Int.Distance(center, pos);
            columns.Add((pos, dist));
        }

        // Sort by distance from center and return positions
        return columns
            .OrderBy(c => c.dist)
            .Select(c => c.pos);
    }

    private async void ProcessQueues()
    {
        // Start new column tasks
        while (columnGenerationQueue.Count > 0 && activeColumnTasks.Count < 64)
        {
            Vector2Int columnPos = columnGenerationQueue.Dequeue();
            var task = ProcessColumnAsync(columnPos);
            activeColumnTasks.Add(task);
        }

        // Wait for any completed tasks
        if (activeColumnTasks.Count > 0)
        {
            await Task.WhenAny(activeColumnTasks);
            activeColumnTasks.RemoveAll(t => t.IsCompleted);
        }

        // Process render queue
        int meshProcessed = 0;
        int maxMeshes = Mathf.Max(32, renderQueue.Count / 4);
        
        while (renderQueue.Count > 0 && meshProcessed < maxMeshes)
        {
            Chunk chunk = renderQueue.Dequeue();
            var mesh = new Mesh();
            meshGenerator.GenerateMeshData(chunk, mesh, currentYLimit);
            chunkRenderer.RenderChunk(chunk, mesh);
            chunk.ClearDirty();
            
            meshProcessed++;
        }
    }

    private async Task ProcessColumnAsync(Vector2Int columnPos)
    {
        columnsInProgress.Add(columnPos);

        try
        {
            var chunksToRender = new List<Chunk>();
            bool foundOpaque = false;
            
            // Start from the Y-level limit and work down
            int startY = Mathf.Min(
                WorldDataManager.WORLD_HEIGHT_IN_CHUNKS - 1, 
                currentYLimit / Chunk.ChunkSize
            );

            for (int y = startY; y >= 0; y--)
            {
                Vector3Int chunkPos = new Vector3Int(columnPos.x, y, columnPos.y);
                
                // Check existing chunk first
                Chunk existingChunk = chunkManager.GetChunk(chunkPos);
                if (existingChunk != null)
                {
                    // If chunk exists and is opaque, stop processing this column
                    if (existingChunk.IsOpaque)
                    {
                        foundOpaque = true;
                        break;
                    }
                        
                    // If chunk exists but needs rendering, add it to render queue
                    if (!chunkRenderer.IsChunkRendered(chunkPos) || existingChunk.IsDirty())
                    {
                        chunksToRender.Add(existingChunk);
                    }
                    continue;  // Skip generation, we already have data
                }
                
                // Only generate if chunk doesn't exist
                Chunk chunk = await worldGenerator.GenerateChunk(chunkPos);
                
                if (chunk != null && !chunk.IsFullyEmpty())
                {
                    chunkManager.StoreChunk(chunk.Position, chunk);
                    chunksToRender.Add(chunk);
                    
                    // If chunk is opaque, stop processing this column
                    if (chunk.IsOpaque)
                    {
                        foundOpaque = true;
                        break;
                    }
                }
            }

            // If we found an opaque chunk, mark this column as complete
            if (foundOpaque)
            {
                completedColumns.Add(columnPos);
            }

            // Add chunks to render queue (they're already in top-down order)
            foreach (var chunk in chunksToRender)
            {
                renderQueue.Enqueue(chunk);
            }
        }
        finally
        {
            columnsInProgress.Remove(columnPos);
        }
    }
}