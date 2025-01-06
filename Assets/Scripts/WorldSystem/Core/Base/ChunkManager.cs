using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using System.Collections.Generic;
using WorldSystem.Data;
using WorldSystem.Jobs;

public class ChunkManager : MonoBehaviour
{
    private const int VERTS_PER_QUAD = 4;
    private const int TRIS_PER_QUAD = 6;
    private const int TOTAL_QUADS = ChunkData.SIZE * ChunkData.SIZE;
    private const int CHUNKS_PER_FRAME = 128;  // Adjust based on performance needs

    [SerializeField] private Camera mainCamera;
    [SerializeField] private Material chunkMaterial;
    [SerializeField] private float loadDistance = 100f;  // World units
    
    private Dictionary<int2, GameObject> _activeChunks = new();
    private Dictionary<int2, byte[]> _chunkDataCache = new();
    private Dictionary<int2, HeightPoint[]> _heightMapCache = new();
    private HashSet<int2> _visibleChunkPositions = new();
    private PriorityQueue<int2> _chunkLoadQueue = new();
    private HashSet<int2> _chunksBeingProcessed = new();
    private List<JobHandle> _pendingJobs = new();
    private NativeArray<BlockDefinition> _blockDefs;
    private Vector3 _lastCameraPosition;
    private float _lastCameraHeight;
    private const float UPDATE_THRESHOLD = 16f; // Only update when camera moves 1 unit

    [SerializeField] private int poolSize = 512; // Adjust based on your max visible chunks
    private Queue<GameObject> _chunkPool;
    
    private const int BATCH_SIZE = 8; // Adjust based on testing
    private List<int2> _batchPositions = new();
    private NativeArray<JobHandle> _batchHandles;
    
    void Start()
    {
        InitializeChunkPool();
        _batchHandles = new NativeArray<JobHandle>(BATCH_SIZE, Allocator.Persistent);
        _blockDefs = new NativeArray<BlockDefinition>(BlockColors.Definitions, Allocator.Persistent);
        UpdateVisibleChunks();
    }

    private void InitializeChunkPool()
    {
        _chunkPool = new Queue<GameObject>();
        
        // Pre-instantiate chunk GameObjects
        for (int i = 0; i < poolSize; i++)
        {
            var chunk = CreateChunkGameObject();
            chunk.SetActive(false);
            _chunkPool.Enqueue(chunk);
        }
    }

    private GameObject CreateChunkGameObject()
    {
        var chunk = new GameObject("Chunk Pool Object");
        chunk.transform.parent = transform;
        
        // Add components that all chunks will need
        chunk.AddComponent<MeshFilter>();
        chunk.AddComponent<MeshRenderer>().material = chunkMaterial;
        
        return chunk;
    }

    void Update()
    {
        Vector3 currentCamPos = mainCamera.transform.position;
        float currentHeight = currentCamPos.y;
        
        // Only update chunks if camera moved enough
        if (Vector3.Distance(_lastCameraPosition, currentCamPos) > UPDATE_THRESHOLD ||
            Mathf.Abs(_lastCameraHeight - currentHeight) > UPDATE_THRESHOLD)
        {
            UpdateVisibleChunks();
            QueueMissingChunks();
            CleanupDistantChunks();
            
            _lastCameraPosition = currentCamPos;
            _lastCameraHeight = currentHeight;
        }

        // This still needs to run every frame to process the queue
        ProcessChunkQueue();
    }

    void UpdateVisibleChunks()
    {
        _visibleChunkPositions.Clear();
        Vector3 camPos = mainCamera.transform.position;
        float camHeight = camPos.y;
        float viewDistance = Mathf.Min(loadDistance, camHeight * 2f); // Adjust view distance based on height

        // Convert camera position to chunk coordinates
        int2 centerChunk = new int2(
            Mathf.FloorToInt(camPos.x / ChunkData.SIZE),
            Mathf.FloorToInt(camPos.z / ChunkData.SIZE)
        );

        // Calculate visible chunks based on view distance
        int chunkDistance = Mathf.CeilToInt(viewDistance / ChunkData.SIZE);
        for (int x = -chunkDistance; x <= chunkDistance; x++)
        for (int z = -chunkDistance; z <= chunkDistance; z++)
        {
            int2 chunkPos = new int2(centerChunk.x + x, centerChunk.y + z);
            _visibleChunkPositions.Add(chunkPos);
        }
    }

    void QueueMissingChunks()
    {
        Vector3 camPos = mainCamera.transform.position;
        foreach (var chunkPos in _visibleChunkPositions)
        {
            if (!_activeChunks.ContainsKey(chunkPos) && 
                !_chunksBeingProcessed.Contains(chunkPos) && 
                !_chunkLoadQueue.Contains(chunkPos))
            {
                float priority = Vector2.Distance(
                    new Vector2(camPos.x, camPos.z),
                    new Vector2(chunkPos.x * ChunkData.SIZE, chunkPos.y * ChunkData.SIZE)
                );
                _chunkLoadQueue.Enqueue(chunkPos, priority);
            }
        }
    }

    void ProcessChunkQueue()
    {
        // Process mesh jobs
        for (int i = _pendingMeshes.Count - 1; i >= 0; i--)
        {
            var pendingMesh = _pendingMeshes[i];
            if (pendingMesh.jobHandle.IsCompleted)
            {
                pendingMesh.jobHandle.Complete();

                var mesh = new Mesh();
                mesh.SetVertices(pendingMesh.vertices.Reinterpret<Vector3>());
                mesh.SetTriangles(pendingMesh.triangles.ToArray(), 0);
                mesh.SetUVs(0, pendingMesh.uvs.Reinterpret<Vector2>());
                mesh.SetColors(pendingMesh.colors.Reinterpret<Color>());
                mesh.RecalculateNormals();

                pendingMesh.meshFilter.mesh = mesh;

                // Cleanup
                pendingMesh.vertices.Dispose();
                pendingMesh.triangles.Dispose();
                pendingMesh.uvs.Dispose();
                pendingMesh.colors.Dispose();
                pendingMesh.heightMap.Dispose();

                _pendingMeshes.RemoveAt(i);
            }
        }

        // Check for completed jobs
        for (int i = _pendingChunks.Count - 1; i >= 0; i--)
        {
            var chunk = _pendingChunks[i];
            if (chunk.jobHandle.IsCompleted)
            {
                chunk.jobHandle.Complete(); // Now safe to access the data
                CreateChunkObject(chunk.position, chunk.blocks, chunk.heightMap);
                
                // Cleanup
                chunk.blocks.Dispose();
                chunk.heightMap.Dispose();
                _pendingChunks.RemoveAt(i);
                _chunksBeingProcessed.Remove(chunk.position);
            }
        }

        // Batch new jobs
        _batchPositions.Clear();
        while (_chunkLoadQueue.Count > 0 && _pendingChunks.Count < CHUNKS_PER_FRAME)
        {
            var pos = _chunkLoadQueue.Dequeue();
            _batchPositions.Add(pos);

            // When we have enough for a batch or no more chunks in queue
            if (_batchPositions.Count >= BATCH_SIZE || _chunkLoadQueue.Count == 0)
            {
                StartChunkGenerationBatch(_batchPositions);
                _batchPositions.Clear();
            }
        }
    }

    private struct PendingChunk
    {
        public int2 position;
        public JobHandle jobHandle;
        public NativeArray<byte> blocks;
        public NativeArray<HeightPoint> heightMap;
    }

    private List<PendingChunk> _pendingChunks = new();

    void StartChunkGenerationBatch(List<int2> positions)
    {
        var batchedChunks = new List<PendingChunk>(positions.Count);

        // Set up all jobs in the batch
        for (int i = 0; i < positions.Count; i++)
        {
            var pos = positions[i];
            _chunksBeingProcessed.Add(pos);

            // Skip if cached (same as before)
            if (_chunkDataCache.TryGetValue(pos, out byte[] cachedBlocks) && 
                _heightMapCache.TryGetValue(pos, out HeightPoint[] cachedHeightMap))
            {
                var cachedHeightMapNative = new NativeArray<HeightPoint>(cachedHeightMap, Allocator.TempJob);
                var cachedBlocksNative = new NativeArray<byte>(cachedBlocks, Allocator.TempJob);
                CreateChunkObject(pos, cachedBlocksNative, cachedHeightMapNative);
                cachedHeightMapNative.Dispose();
                cachedBlocksNative.Dispose();
                _chunksBeingProcessed.Remove(pos);
                continue;
            }

            // Prepare job data
            var blocks = new NativeArray<byte>(ChunkData.SIZE * ChunkData.SIZE * ChunkData.SIZE, Allocator.TempJob);
            var heightMap = new NativeArray<HeightPoint>(ChunkData.SIZE * ChunkData.SIZE, Allocator.TempJob);

            var genJob = new ChunkGenerationJob
            {
                position = pos,
                seed = 123,
                blocks = blocks,
                heightMap = heightMap
            };

            // Store job handle in the batch array
            _batchHandles[i] = genJob.Schedule();

            batchedChunks.Add(new PendingChunk
            {
                position = pos,
                blocks = blocks,
                heightMap = heightMap
            });
        }

        // Combine all job handles in the batch
        var combinedHandle = JobHandle.CombineDependencies(_batchHandles.Slice(0, positions.Count));

        // Update pending chunks with combined handle
        foreach (var chunk in batchedChunks)
        {
            _pendingChunks.Add(new PendingChunk
            {
                position = chunk.position,
                jobHandle = combinedHandle,
                blocks = chunk.blocks,
                heightMap = chunk.heightMap
            });
        }
    }

    void CreateChunkObject(int2 position, NativeArray<byte> blocks, NativeArray<HeightPoint> heightMap)
    {
        GameObject chunkObject;
        
        // Try to get from pool, create new if pool is empty
        if (_chunkPool.Count > 0)
        {
            chunkObject = _chunkPool.Dequeue();
            chunkObject.SetActive(true);
        }
        else
        {
            Debug.LogWarning("Chunk pool depleted, creating new chunk object");
            chunkObject = CreateChunkGameObject();
        }

        // Configure the chunk
        chunkObject.name = $"Chunk {position.x},{position.y}";
        chunkObject.transform.position = new Vector3(position.x * ChunkData.SIZE, 0, position.y * ChunkData.SIZE);

        var meshFilter = chunkObject.GetComponent<MeshFilter>();
        
        // Create a copy of heightMap for the mesh job
        var heightMapCopy = new NativeArray<HeightPoint>(heightMap.Length, Allocator.TempJob);
        heightMapCopy.CopyFrom(heightMap);
        
        CreateMesh(position, heightMapCopy, meshFilter);
        
        _activeChunks[position] = chunkObject;
        _chunkDataCache[position] = blocks.ToArray();
        _heightMapCache[position] = heightMap.ToArray();
    }

    private struct PendingMesh
    {
        public int2 position;
        public JobHandle jobHandle;
        public NativeArray<float3> vertices;
        public NativeArray<int> triangles;
        public NativeArray<float2> uvs;
        public NativeArray<float4> colors;
        public NativeArray<HeightPoint> heightMap;
        public GameObject gameObject;
        public MeshFilter meshFilter;
    }

    private List<PendingMesh> _pendingMeshes = new();

    private void CreateMesh(int2 position, NativeArray<HeightPoint> heightMap, MeshFilter meshFilter)
    {
        var vertices = new NativeArray<float3>(TOTAL_QUADS * VERTS_PER_QUAD, Allocator.TempJob);
        var triangles = new NativeArray<int>(TOTAL_QUADS * TRIS_PER_QUAD, Allocator.TempJob);
        var uvs = new NativeArray<float2>(TOTAL_QUADS * VERTS_PER_QUAD, Allocator.TempJob);
        var colors = new NativeArray<float4>(TOTAL_QUADS * VERTS_PER_QUAD, Allocator.TempJob);

        var meshJob = new ChunkMeshJob
        {
            heightMap = heightMap,
            chunkPosition = position,
            vertices = vertices,
            triangles = triangles,
            uvs = uvs,
            colors = colors,
            blockDefinitions = _blockDefs
        };

        var jobHandle = meshJob.Schedule();
        
        _pendingMeshes.Add(new PendingMesh
        {
            position = position,
            jobHandle = jobHandle,
            vertices = vertices,
            triangles = triangles,
            uvs = uvs,
            colors = colors,
            gameObject = meshFilter.gameObject,
            meshFilter = meshFilter,
            heightMap = heightMap  // Store heightMap to dispose later
        });
    }

    void CleanupDistantChunks()
    {
        var chunksToRemove = new List<int2>();
        foreach (var chunk in _activeChunks)
        {
            if (!_visibleChunkPositions.Contains(chunk.Key))
            {
                chunksToRemove.Add(chunk.Key);
            }
        }

        foreach (var pos in chunksToRemove)
        {
            var chunk = _activeChunks[pos];
            chunk.SetActive(false);
            _chunkPool.Enqueue(chunk); // Return to pool
            _activeChunks.Remove(pos);
        }

        // Clean pending chunks
        for (int i = _pendingChunks.Count - 1; i >= 0; i--)
        {
            if (!_visibleChunkPositions.Contains(_pendingChunks[i].position))
            {
                var chunk = _pendingChunks[i];
                chunk.jobHandle.Complete(); // Must complete before disposing
                chunk.blocks.Dispose();
                chunk.heightMap.Dispose();
                _pendingChunks.RemoveAt(i);
                _chunksBeingProcessed.Remove(chunk.position);
            }
        }

        // Clean queue
        var newQueue = new PriorityQueue<int2>();
        while (_chunkLoadQueue.Count > 0)
        {
            var pos = _chunkLoadQueue.Dequeue();
            if (_visibleChunkPositions.Contains(pos))
            {
                newQueue.Enqueue(pos, Vector2.Distance(
                    new Vector2(mainCamera.transform.position.x, mainCamera.transform.position.z),
                    new Vector2(pos.x * ChunkData.SIZE, pos.y * ChunkData.SIZE)
                ));
            }
        }
        _chunkLoadQueue = newQueue;
    }

    void OnDestroy()
    {
        // Complete any pending jobs
        foreach (var job in _pendingJobs)
        {
            job.Complete();
        }

        _blockDefs.Dispose();
        _activeChunks.Clear();
        _chunkDataCache.Clear();
        _heightMapCache.Clear();

        // Clean up pool
        while (_chunkPool.Count > 0)
        {
            var chunk = _chunkPool.Dequeue();
            if (chunk != null)
                Destroy(chunk);
        }

        if (_batchHandles.IsCreated)
            _batchHandles.Dispose();
    }
} 