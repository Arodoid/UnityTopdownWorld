using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using VoxelGame.Interfaces;
using VoxelGame.WorldSystem;
using VoxelGame.WorldSystem.Generation.Core;  // For WorldGenerator
using VoxelGame.Utilities;

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

    private void Update() 
    {
        // Process queues only if necessary
        if (columnGenerationQueue.Count > 0 || renderQueue.Count > 0)
        {
            ProcessQueues();
        }
    }

    public void UpdateVisibleChunks(CameraVisibilityController.ViewData viewData)
    {
        PerformanceMonitor.StartMeasurement("ChunkProcessor.UpdateVisibleChunks.Total");
        
        PerformanceMonitor.StartMeasurement("ChunkProcessor.UpdateVisibleChunks.QueueClear");
        columnGenerationQueue.Clear();
        renderQueue.Clear();
        PerformanceMonitor.EndMeasurement("ChunkProcessor.UpdateVisibleChunks.QueueClear");
        
        currentYLimit = viewData.YLevel;
        var columnsInView = GetColumnsInView(viewData.ViewBounds);
        var columnsToKeep = new HashSet<Vector2Int>(columnsInView);

        PerformanceMonitor.StartMeasurement("ChunkProcessor.UpdateVisibleChunks.Cleanup");
        // Clean up chunks outside view
        foreach (var chunkPos in chunkManager.GetAllChunkPositions().ToList())
        {
            Vector2Int columnPos = new Vector2Int(chunkPos.x, chunkPos.z);
            if (!columnsToKeep.Contains(columnPos))
            {
                if (meshGenerator.HasPendingJob(chunkPos))
                {
                    meshGenerator.CancelJob(chunkPos);
                }
                chunkRenderer.RemoveChunkRender(chunkPos);
                chunkManager.RemoveChunk(chunkPos);
                completedColumns.Remove(columnPos);
            }
        }
        PerformanceMonitor.EndMeasurement("ChunkProcessor.UpdateVisibleChunks.Cleanup");

        PerformanceMonitor.StartMeasurement("ChunkProcessor.UpdateVisibleChunks.QueueNew");
        // Queue new chunks for generation
        foreach (var columnPos in columnsInView)
        {
            if (!completedColumns.Contains(columnPos) && !columnsInProgress.Contains(columnPos))
            {
                columnGenerationQueue.Enqueue(columnPos);
            }
        }
        PerformanceMonitor.EndMeasurement("ChunkProcessor.UpdateVisibleChunks.QueueNew");
        
        PerformanceMonitor.EndMeasurement("ChunkProcessor.UpdateVisibleChunks.Total");
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
        PerformanceMonitor.StartMeasurement("ChunkProcessor.ProcessQueues.Total");
        
        PerformanceMonitor.StartMeasurement("ChunkProcessor.ProcessQueues.UpdateLimits");
        processingLimits.UpdateLimits();
        PerformanceMonitor.EndMeasurement("ChunkProcessor.ProcessQueues.UpdateLimits");

        // Process render queue first (non-async)
        int meshesProcessed = 0;
        
        PerformanceMonitor.StartMeasurement("ChunkProcessor.ProcessQueues.RenderLoop");
        while (renderQueue.Count > 0 && meshesProcessed < processingLimits.MaxMeshesPerFrame)
        {
            PerformanceMonitor.StartMeasurement("ChunkProcessor.ProcessQueues.DequeueAndCheck");
            Chunk chunk = renderQueue.Dequeue();
            if (chunk == null || chunk.IsFullyEmpty())
            {
                PerformanceMonitor.EndMeasurement("ChunkProcessor.ProcessQueues.DequeueAndCheck");
                continue;
            }
            PerformanceMonitor.EndMeasurement("ChunkProcessor.ProcessQueues.DequeueAndCheck");

            PerformanceMonitor.StartMeasurement("ChunkProcessor.ProcessQueues.JobCheck");
            if (!meshGenerator.HasPendingJob(chunk.Position))
            {
                PerformanceMonitor.EndMeasurement("ChunkProcessor.ProcessQueues.JobCheck");
                
                PerformanceMonitor.StartMeasurement("ChunkProcessor.ProcessQueues.MeshCreation");
                var mesh = new Mesh();
                
                // If rejected, put back in queue
                if (!meshGenerator.GenerateMeshData(chunk, mesh, currentYLimit))
                {
                    renderQueue.Enqueue(chunk);
                    Object.Destroy(mesh);
                    PerformanceMonitor.EndMeasurement("ChunkProcessor.ProcessQueues.MeshCreation");
                    break; // Stop processing if we're hitting limits
                }
                
                // Render the chunk with the new mesh
                chunkRenderer.RenderChunk(chunk, mesh);
                meshesProcessed++;
                PerformanceMonitor.EndMeasurement("ChunkProcessor.ProcessQueues.MeshCreation");
            }
            else
            {
                PerformanceMonitor.EndMeasurement("ChunkProcessor.ProcessQueues.JobCheck");
                renderQueue.Enqueue(chunk);
                break;
            }
        }
        PerformanceMonitor.EndMeasurement("ChunkProcessor.ProcessQueues.RenderLoop");

        // Then process column generation (async)
        PerformanceMonitor.StartMeasurement("ChunkProcessor.ProcessQueues.ColumnGeneration");
        ProcessColumnGeneration();
        PerformanceMonitor.EndMeasurement("ChunkProcessor.ProcessQueues.ColumnGeneration");
        
        PerformanceMonitor.EndMeasurement("ChunkProcessor.ProcessQueues.Total");
    }

    private async void ProcessColumnGeneration()
    {
        PerformanceMonitor.StartMeasurement("ChunkProcessor.ColumnGeneration.Total");
        
        PerformanceMonitor.StartMeasurement("ChunkProcessor.ColumnGeneration.BatchPrep");
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
        PerformanceMonitor.EndMeasurement("ChunkProcessor.ColumnGeneration.BatchPrep");

        if (columnBatch.Count > 0)
        {
            PerformanceMonitor.StartMeasurement("ChunkProcessor.ColumnGeneration.ProcessBatch");
            var tasks = columnBatch.Select(columnPos => ProcessColumnAsync(columnPos));
            await Task.WhenAll(tasks);
            PerformanceMonitor.EndMeasurement("ChunkProcessor.ColumnGeneration.ProcessBatch");
        }
        
        PerformanceMonitor.EndMeasurement("ChunkProcessor.ColumnGeneration.Total");
    }

    private async Task ProcessColumnAsync(Vector2Int columnPos)
    {
        PerformanceMonitor.StartMeasurement("ChunkProcessor.ProcessColumn.Total");
        if (columnsInProgress.Contains(columnPos) || worldGenerator == null)
            return;

        columnsInProgress.Add(columnPos);

        try
        {
            var chunksToRender = new List<Chunk>(8);
            bool foundOpaque = false;
            
            PerformanceMonitor.StartMeasurement("ChunkProcessor.ProcessColumn.Setup");
            int maxY = Mathf.Min(
                WorldDataManager.WORLD_HEIGHT_IN_CHUNKS - 1,
                Mathf.CeilToInt(currentYLimit / (float)Chunk.ChunkSize)
            );
            PerformanceMonitor.EndMeasurement("ChunkProcessor.ProcessColumn.Setup");

            PerformanceMonitor.StartMeasurement("ChunkProcessor.ProcessColumn.ChunkLoop");
            int chunksProcessed = 0;
            
            // Process from top down and exit early if we find an opaque chunk
            for (int y = maxY; y >= 0 && !foundOpaque; y--)
            {
                chunksProcessed++;
                Vector3Int chunkPos = new Vector3Int(columnPos.x, y, columnPos.y);
                
                PerformanceMonitor.StartMeasurement("ChunkProcessor.ProcessColumn.GetChunk");
                Chunk chunk = chunkManager.GetChunk(chunkPos);
                PerformanceMonitor.EndMeasurement("ChunkProcessor.ProcessColumn.GetChunk");
                
                if (chunk == null)
                {
                    PerformanceMonitor.StartMeasurement("ChunkProcessor.ProcessColumn.GenerateChunk");
                    chunk = await worldGenerator.GenerateChunk(chunkPos);
                    PerformanceMonitor.EndMeasurement("ChunkProcessor.ProcessColumn.GenerateChunk");
                    
                    if (chunk == null) continue;
                    
                    PerformanceMonitor.StartMeasurement("ChunkProcessor.ProcessColumn.StoreChunk");
                    chunkManager.StoreChunk(chunk.Position, chunk);
                    PerformanceMonitor.EndMeasurement("ChunkProcessor.ProcessColumn.StoreChunk");
                }
                
                PerformanceMonitor.StartMeasurement("ChunkProcessor.ProcessColumn.ProcessChunk");
                if (!chunk.IsFullyEmpty())
                {
                    chunksToRender.Add(chunk);
                    foundOpaque = chunk.IsOpaque;
                }
                PerformanceMonitor.EndMeasurement("ChunkProcessor.ProcessColumn.ProcessChunk");
            }
            PerformanceMonitor.EndMeasurement("ChunkProcessor.ProcessColumn.ChunkLoop");

            PerformanceMonitor.StartMeasurement("ChunkProcessor.ProcessColumn.QueueChunks");
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
                completedColumns.Add(columnPos);
            }
            PerformanceMonitor.EndMeasurement("ChunkProcessor.ProcessColumn.QueueChunks");
                    }
        finally
        {
            columnsInProgress.Remove(columnPos);
        }
        PerformanceMonitor.EndMeasurement("ChunkProcessor.ProcessColumn.Total");
    }
}