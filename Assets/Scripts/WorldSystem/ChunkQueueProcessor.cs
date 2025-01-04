using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using VoxelGame.Interfaces;
using VoxelGame.WorldSystem;
using VoxelGame.WorldSystem.Generation.Core;  // For WorldGenerator

public class ChunkQueueProcessor : MonoBehaviour
{
    [SerializeField] private int maxChunksPerFrame = 8;

    private IChunkGenerator worldGenerator;
    private IChunkManager chunkManager;
    private IChunkMeshGenerator meshGenerator;
    private IChunkRenderer chunkRenderer;

    private Queue<Vector2Int> columnGenerationQueue = new Queue<Vector2Int>();
    private Queue<Chunk> renderQueue = new Queue<Chunk>();
    private HashSet<Vector2Int> columnsInProgress = new HashSet<Vector2Int>();
    private HashSet<Vector2Int> completedColumns = new HashSet<Vector2Int>();
    private int currentYLimit = int.MaxValue;

    private class DynamicProcessingLimits
    {
        public int MaxColumnsPerFrame { get; private set; } = 16;
        public int MaxMeshesPerFrame { get; private set; } = 64;
        private float lastUpdateTime;
        private const float UPDATE_INTERVAL = 0.1f;
        private const float TARGET_FRAME_TIME = 1f / 60f;

        public void UpdateLimits()
        {
            if (Time.realtimeSinceStartup - lastUpdateTime < UPDATE_INTERVAL) return;
            
            float frameTime = Time.deltaTime;
            float headroom = TARGET_FRAME_TIME - frameTime;
            
            if (frameTime > TARGET_FRAME_TIME * 1.2f)
            {
                MaxColumnsPerFrame = Mathf.Max(4, MaxColumnsPerFrame - 4);
                MaxMeshesPerFrame = Mathf.Max(16, MaxMeshesPerFrame - 8);
            }
            else if (headroom > TARGET_FRAME_TIME * 0.3f)
            {
                MaxColumnsPerFrame = Mathf.Min(32, MaxColumnsPerFrame + 2);
                MaxMeshesPerFrame = Mathf.Min(128, MaxMeshesPerFrame + 4);
            }
            
            lastUpdateTime = Time.realtimeSinceStartup;
        }
    }

    private DynamicProcessingLimits processingLimits = new DynamicProcessingLimits();

    private void Awake()
    {
        // Get concrete implementations first
        var worldGeneratorComponent = GetComponent<WorldGenerator>();
        var chunkManagerComponent = GetComponent<ChunkManager>();
        var meshGeneratorComponent = GetComponent<ChunkMeshGenerator>();
        var chunkRendererComponent = GetComponent<ChunkRenderer>();

        // Then assign them to the interface references
        worldGenerator = worldGeneratorComponent;
        chunkManager = chunkManagerComponent;
        meshGenerator = meshGeneratorComponent;
        chunkRenderer = chunkRendererComponent;

        string missingComponents = "";
        if (worldGeneratorComponent == null) missingComponents += "WorldGenerator, ";
        if (chunkManagerComponent == null) missingComponents += "ChunkManager, ";
        if (meshGeneratorComponent == null) missingComponents += "ChunkMeshGenerator, ";
        if (chunkRendererComponent == null) missingComponents += "ChunkRenderer, ";

        if (!string.IsNullOrEmpty(missingComponents))
        {
            missingComponents = missingComponents.TrimEnd(',', ' ');
            Debug.LogError($"Missing required components on ChunkQueueProcessor: {missingComponents}");
        }
    }

    private void Update() => ProcessQueues();

    public void UpdateVisibleChunks(CameraVisibilityController.ViewData viewData)
    {
        columnGenerationQueue.Clear();
        renderQueue.Clear();
        
        currentYLimit = viewData.YLevel;
        var columnsInView = GetColumnsInView(viewData.ViewBounds);
        var columnsToKeep = new HashSet<Vector2Int>(columnsInView);

        // Clean up chunks outside view
        foreach (var chunkPos in chunkManager.GetAllChunkPositions().ToList())
        {
            Vector2Int columnPos = new Vector2Int(chunkPos.x, chunkPos.z);
            if (!columnsToKeep.Contains(columnPos))
            {
                // Cancel any pending mesh generation jobs before removing the chunk
                if (meshGenerator.HasPendingJob(chunkPos))
                {
                    meshGenerator.CancelJob(chunkPos);
                }
                chunkRenderer.RemoveChunkRender(chunkPos);
                chunkManager.RemoveChunk(chunkPos);
                completedColumns.Remove(columnPos);
            }
        }

        // Queue new chunks for generation
        foreach (var columnPos in columnsInView)
        {
            if (!completedColumns.Contains(columnPos) && !columnsInProgress.Contains(columnPos))
            {
                columnGenerationQueue.Enqueue(columnPos);
            }
        }
    }

    private bool ShouldRenderChunk(Chunk chunk) =>
        !chunk.IsFullyEmpty() && (!chunkRenderer.IsChunkRendered(chunk.Position) || chunk.IsDirty());

    private IEnumerable<Vector2Int> GetColumnsInView(Bounds viewBounds)
    {
        int minX = Mathf.FloorToInt(viewBounds.min.x / Chunk.ChunkSize);
        int maxX = Mathf.CeilToInt(viewBounds.max.x / Chunk.ChunkSize);
        int minZ = Mathf.FloorToInt(viewBounds.min.z / Chunk.ChunkSize);
        int maxZ = Mathf.CeilToInt(viewBounds.max.z / Chunk.ChunkSize);

        Vector2Int center = new Vector2Int(
            Mathf.RoundToInt(viewBounds.center.x / Chunk.ChunkSize),
            Mathf.RoundToInt(viewBounds.center.z / Chunk.ChunkSize)
        );

        var columns = new List<(Vector2Int pos, float dist)>();
        
        for (int x = minX; x <= maxX; x++)
        for (int z = minZ; z <= maxZ; z++)
        {
            Vector2Int pos = new Vector2Int(x, z);
            float dist = Vector2Int.Distance(center, pos);
            columns.Add((pos, dist));
        }

        return columns.OrderBy(c => c.dist).Select(c => c.pos);
    }

    private void ProcessQueues()
    {
        processingLimits.UpdateLimits();

        // Process render queue first (non-async)
        int meshesProcessed = 0;
        while (renderQueue.Count > 0 && meshesProcessed < processingLimits.MaxMeshesPerFrame)
        {
            Chunk chunk = renderQueue.Dequeue();
            if (chunk != null && !chunk.IsFullyEmpty())
            {
                if (!meshGenerator.HasPendingJob(chunk.Position))
                {
                    // Create a new mesh for this chunk
                    var mesh = new Mesh();
                    
                    // If rejected, put back in queue
                    if (!meshGenerator.GenerateMeshData(chunk, mesh, currentYLimit))
                    {
                        renderQueue.Enqueue(chunk);
                        Object.Destroy(mesh);
                        break; // Stop processing if we're hitting limits
                    }
                    
                    // Render the chunk with the new mesh
                    chunkRenderer.RenderChunk(chunk, mesh);
                    meshesProcessed++;
                }
                else
                {
                    renderQueue.Enqueue(chunk);
                    break;
                }
            }
        }

        // Then process column generation (async)
        ProcessColumnGeneration();
    }

    private async void ProcessColumnGeneration()
    {
        var columnBatch = new List<Vector2Int>();
        int batchSize = Mathf.Min(4, processingLimits.MaxColumnsPerFrame);
        
        while (columnGenerationQueue.Count > 0 && columnBatch.Count < batchSize)
        {
            var columnPos = columnGenerationQueue.Dequeue();
            if (!columnsInProgress.Contains(columnPos))
            {
                columnBatch.Add(columnPos);
            }
        }

        if (columnBatch.Count > 0)
        {
            var tasks = columnBatch.Select(columnPos => ProcessColumnAsync(columnPos));
            await Task.WhenAll(tasks);
        }
    }

    private async Task ProcessColumnAsync(Vector2Int columnPos)
    {
        if (columnsInProgress.Contains(columnPos) || worldGenerator == null)
            return;

        columnsInProgress.Add(columnPos);

        try
        {
            var chunksToRender = new List<Chunk>(8); // Preallocate with expected size
            bool foundOpaque = false;
            
            int maxY = Mathf.Min(
                WorldDataManager.WORLD_HEIGHT_IN_CHUNKS - 1,
                Mathf.CeilToInt(currentYLimit / (float)Chunk.ChunkSize)
            );

            // Process from top down and exit early if we find an opaque chunk
            for (int y = maxY; y >= 0 && !foundOpaque; y--)
            {
                Vector3Int chunkPos = new Vector3Int(columnPos.x, y, columnPos.y);
                Chunk chunk = chunkManager.GetChunk(chunkPos);
                
                if (chunk == null)
                {
                    chunk = await worldGenerator.GenerateChunk(chunkPos);
                    if (chunk == null) continue;
                    chunkManager.StoreChunk(chunk.Position, chunk);
                }
                
                if (!chunk.IsFullyEmpty())
                {
                    chunksToRender.Add(chunk);
                    foundOpaque = chunk.IsOpaque;
                }
            }

            // Batch process chunks that need rendering
            if (chunksToRender.Count > 0)
            {
                foreach (var chunk in chunksToRender)
                {
                    renderQueue.Enqueue(chunk);
                }

                if (foundOpaque)
                {
                    completedColumns.Add(columnPos);
                }
            }
            else
            {
                completedColumns.Add(columnPos); // Mark empty columns as completed
            }
        }
        finally
        {
            columnsInProgress.Remove(columnPos);
        }
    }
}