using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using System.Collections.Generic;
using WorldSystem.Data;
using WorldSystem.Jobs;
using System.Linq;

public class ChunkManager : MonoBehaviour
{
    private const int VERTS_PER_QUAD = 4;
    private const int TRIS_PER_QUAD = 6;
    private const int TOTAL_QUADS = ChunkData.SIZE * ChunkData.SIZE;
    private const int CHUNKS_PER_FRAME = 32;  // Adjust based on performance needs

    [SerializeField] private Camera mainCamera;
    [SerializeField] private Material chunkMaterial;

    [SerializeField] private float loadDistance = 100f;  // Fallback for perspective camera if orthographic is false for whatever reason
    [SerializeField] private float chunkLoadBuffer = 1.2f; // 1.0 = exact fit, 1.2 = 20% extra, etc.
    
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
    private const float UPDATE_THRESHOLD = 16f; // Only update when camera moves 1 chunk

    [SerializeField] private int poolSize = 512; // Adjust based on your max visible chunks
    private Queue<GameObject> _chunkPool;
    
        private const int BATCH_SIZE = 256; // Adjust based on testing
    private List<int2> _batchPositions = new();
    private NativeArray<JobHandle> _batchHandles;
    
    // Add new field to track last ortho size
    private float _lastOrthoSize;

    [SerializeField] private float bufferTimeSeconds = 10f; // How long chunks stay in memory after leaving view
    private Dictionary<int2, (float timestamp, GameObject gameObject)> _inactiveChunks = new(); // Tracks chunks in buffer zone
    private const float BUFFER_CHECK_INTERVAL = 1f; // How often to check buffer (optimization)
    private float _lastBufferCheck;

    [SerializeField] private int maxChunks = 512; // Maximum total chunks that can exist
    private int TotalChunksInUse => _activeChunks.Count + _inactiveChunks.Count + _chunkPool.Count;

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
        
        // Add components for render mesh
        chunk.AddComponent<MeshFilter>();
        chunk.AddComponent<MeshRenderer>().material = chunkMaterial;
        
        // Add components for shadow mesh
        var shadowObject = new GameObject("Shadow Mesh");
        shadowObject.transform.parent = chunk.transform;
        shadowObject.transform.localPosition = Vector3.zero;
        var shadowMeshFilter = shadowObject.AddComponent<MeshFilter>();
        var shadowMeshRenderer = shadowObject.AddComponent<MeshRenderer>();
        shadowMeshRenderer.material = chunkMaterial;
        shadowMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        shadowMeshRenderer.receiveShadows = false;
        
        return chunk;
    }

    void Update()
    {
        Vector3 currentCamPos = mainCamera.transform.position;
        float currentHeight = currentCamPos.y;
        float currentOrthoSize = mainCamera.orthographicSize;
        
        // Only update chunks if camera moved enough or ortho size changed
        if (Vector3.Distance(_lastCameraPosition, currentCamPos) > UPDATE_THRESHOLD ||
            Mathf.Abs(_lastCameraHeight - currentHeight) > UPDATE_THRESHOLD ||
            Mathf.Abs(_lastOrthoSize - currentOrthoSize) > 0.01f)  // Small threshold for ortho changes
        {
            UpdateVisibleChunks();
            QueueMissingChunks();
            CleanupDistantChunks();
            
            _lastCameraPosition = currentCamPos;
            _lastCameraHeight = currentHeight;
            _lastOrthoSize = currentOrthoSize;
        }

        // This still needs to run every frame to process the queue
        ProcessChunkQueue();
    }

    void UpdateVisibleChunks()
    {
        _visibleChunkPositions.Clear();
        Vector3 camPos = mainCamera.transform.position;
        
        if (mainCamera.orthographic)
        {
            float aspect = mainCamera.aspect;
            float orthoWidth = mainCamera.orthographicSize * 2f * aspect;
            float orthoHeight = mainCamera.orthographicSize * 2f;
            
            // Apply buffer to the view distances
            orthoWidth *= chunkLoadBuffer;
            orthoHeight *= chunkLoadBuffer;
            
            // Calculate chunk distances for width and height separately
            int chunkDistanceX = Mathf.CeilToInt((orthoWidth * 0.5f) / ChunkData.SIZE);
            int chunkDistanceZ = Mathf.CeilToInt((orthoHeight * 0.5f) / ChunkData.SIZE);

            // Convert camera position to chunk coordinates
            int2 centerChunk = new int2(
                Mathf.FloorToInt(camPos.x / ChunkData.SIZE),
                Mathf.FloorToInt(camPos.z / ChunkData.SIZE)
            );

            // Use different ranges for X and Z to create a rectangular area
            for (int x = -chunkDistanceX; x <= chunkDistanceX; x++)
            for (int z = -chunkDistanceZ; z <= chunkDistanceZ; z++)
            {
                int2 chunkPos = new int2(centerChunk.x + x, centerChunk.y + z);
                _visibleChunkPositions.Add(chunkPos);
            }
        }
        else
        {
            // Fallback for perspective camera (using the buffer as a direct multiplier)
            float viewDistance = Mathf.Min(loadDistance, camPos.y * 2f) * chunkLoadBuffer;
            int2 centerChunk = new int2(
                Mathf.FloorToInt(camPos.x / ChunkData.SIZE),
                Mathf.FloorToInt(camPos.z / ChunkData.SIZE)
            );
            
            int chunkDistance = Mathf.CeilToInt(viewDistance / ChunkData.SIZE);
            for (int x = -chunkDistance; x <= chunkDistance; x++)
            for (int z = -chunkDistance; z <= chunkDistance; z++)
            {
                int2 chunkPos = new int2(centerChunk.x + x, centerChunk.y + z);
                _visibleChunkPositions.Add(chunkPos);
            }
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

                // Create render mesh
                var mesh = new Mesh();
                mesh.SetVertices(pendingMesh.vertices.Reinterpret<Vector3>());
                mesh.SetTriangles(pendingMesh.triangles.ToArray(), 0);
                mesh.SetUVs(0, pendingMesh.uvs.Reinterpret<Vector2>());
                mesh.SetColors(pendingMesh.colors.Reinterpret<Color>());
                mesh.RecalculateNormals();
                pendingMesh.meshFilter.mesh = mesh;

                // Create shadow mesh
                var shadowMesh = new Mesh();
                shadowMesh.SetVertices(pendingMesh.shadowVertices.Reinterpret<Vector3>());
                shadowMesh.SetTriangles(pendingMesh.shadowTriangles.ToArray(), 0);
                shadowMesh.RecalculateNormals();
                pendingMesh.shadowMeshFilter.mesh = shadowMesh;

                // Cleanup ALL native arrays
                pendingMesh.vertices.Dispose();
                pendingMesh.triangles.Dispose();
                pendingMesh.uvs.Dispose();
                pendingMesh.colors.Dispose();
                pendingMesh.meshCounts.Dispose();
                pendingMesh.heightMap.Dispose();
                pendingMesh.shadowVertices.Dispose();
                pendingMesh.shadowTriangles.Dispose();
                pendingMesh.shadowMeshCounts.Dispose();

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

        for (int i = 0; i < positions.Count; i++)
        {
            var pos = positions[i];
            _chunksBeingProcessed.Add(pos);

            if (_chunkDataCache.TryGetValue(pos, out byte[] cachedBlocks) && 
                _heightMapCache.TryGetValue(pos, out HeightPoint[] cachedHeightMap))
            {
                // Use Persistent allocator for cached data since it needs to survive until job completion
                var cachedHeightMapNative = new NativeArray<HeightPoint>(cachedHeightMap, Allocator.Persistent);
                var cachedBlocksNative = new NativeArray<byte>(cachedBlocks, Allocator.Persistent);
                CreateChunkObject(pos, cachedBlocksNative, cachedHeightMapNative);
                _chunksBeingProcessed.Remove(pos);
                continue;
            }

            // Use Persistent allocator for job data
            var blocks = new NativeArray<byte>(ChunkData.SIZE * ChunkData.SIZE * ChunkData.SIZE, Allocator.Persistent);
            var heightMap = new NativeArray<HeightPoint>(ChunkData.SIZE * ChunkData.SIZE, Allocator.Persistent);

            var genJob = new ChunkGenerationJob
            {
                position = pos,
                seed = 123,
                blocks = blocks,
                heightMap = heightMap
            };

            _batchHandles[i] = genJob.Schedule(ChunkData.SIZE * ChunkData.SIZE, 64);

            batchedChunks.Add(new PendingChunk
            {
                position = pos,
                blocks = blocks,
                heightMap = heightMap
            });
        }

        // Combine all job handles
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
        
        // First, try to force-reclaim chunks if we're at the limit
        if (_chunkPool.Count == 0 && TotalChunksInUse >= maxChunks)
        {
            // Force immediate cleanup of oldest inactive chunks
            var oldestChunks = _inactiveChunks
                .OrderBy(x => x.Value.timestamp)
                .Take(maxChunks / 4) // Reclaim 25% of max chunks
                .ToList();

            foreach (var old in oldestChunks)
            {
                _chunkPool.Enqueue(old.Value.gameObject);
                _inactiveChunks.Remove(old.Key);
            }
        }

        // Now try to get a chunk
        if (_inactiveChunks.TryGetValue(position, out var inactive))
        {
            chunkObject = inactive.gameObject;
            _inactiveChunks.Remove(position);
            chunkObject.SetActive(true);
        }
        else if (_chunkPool.Count > 0)
        {
            chunkObject = _chunkPool.Dequeue();
            chunkObject.SetActive(true);
        }
        else
        {
            // If we still can't get a chunk, skip this one
            Debug.LogWarning($"Cannot create chunk at {position}: At maximum chunk limit ({maxChunks}). Consider increasing maxChunks if needed.");
            return;
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
        public NativeArray<int> meshCounts;
        public NativeArray<float3> shadowVertices;
        public NativeArray<int> shadowTriangles;
        public NativeArray<int> shadowMeshCounts;
        public MeshFilter shadowMeshFilter;
    }

    private List<PendingMesh> _pendingMeshes = new();

    private void CreateMesh(int2 position, NativeArray<HeightPoint> heightMap, MeshFilter meshFilter)
    {
        // Change allocator from TempJob to Persistent since these arrays need to live longer
        var vertices = new NativeArray<float3>(TOTAL_QUADS * VERTS_PER_QUAD, Allocator.Persistent);
        var triangles = new NativeArray<int>(TOTAL_QUADS * TRIS_PER_QUAD, Allocator.Persistent);
        var uvs = new NativeArray<float2>(TOTAL_QUADS * VERTS_PER_QUAD, Allocator.Persistent);
        var colors = new NativeArray<float4>(TOTAL_QUADS * VERTS_PER_QUAD, Allocator.Persistent);
        var meshCounts = new NativeArray<int>(ChunkData.SIZE * 4, Allocator.Persistent);

        // Add shadow mesh arrays
        var shadowVertices = new NativeArray<float3>(TOTAL_QUADS * VERTS_PER_QUAD * 4, Allocator.Persistent);
        var shadowTriangles = new NativeArray<int>(TOTAL_QUADS * TRIS_PER_QUAD * 4, Allocator.Persistent);

        var meshJob = new ChunkMeshJob
        {
            heightMap = heightMap,
            chunkPosition = position,
            vertices = vertices,
            triangles = triangles,
            uvs = uvs,
            colors = colors,
            blockDefinitions = _blockDefs,
            meshCounts = meshCounts,
            shadowVertices = shadowVertices,
            shadowTriangles = shadowTriangles
        };

        var jobHandle = meshJob.Schedule(ChunkData.SIZE, 1);
        
        var shadowMeshFilter = meshFilter.gameObject.transform.GetChild(0).GetComponent<MeshFilter>();
        
        _pendingMeshes.Add(new PendingMesh
        {
            position = position,
            jobHandle = jobHandle,
            vertices = vertices,
            triangles = triangles,
            uvs = uvs,
            colors = colors,
            meshFilter = meshFilter,
            heightMap = heightMap,
            meshCounts = meshCounts,
            shadowVertices = shadowVertices,
            shadowTriangles = shadowTriangles,
            shadowMeshFilter = shadowMeshFilter
        });
    }

    void CleanupDistantChunks()
    {
        var currentTime = Time.time;
        
        // Process chunks that just left view
        var chunksToRemove = new List<int2>();
        foreach (var chunk in _activeChunks)
        {
            if (!_visibleChunkPositions.Contains(chunk.Key))
            {
                chunksToRemove.Add(chunk.Key);
            }
        }

        // Move them to inactive buffer instead of immediately pooling
        foreach (var pos in chunksToRemove)
        {
            var chunk = _activeChunks[pos];
            _inactiveChunks[pos] = (currentTime, chunk); // Store both timestamp and GameObject
            chunk.SetActive(false);
            _activeChunks.Remove(pos);
        }

        // Only check buffer periodically to save performance
        if (currentTime - _lastBufferCheck > BUFFER_CHECK_INTERVAL)
        {
            _lastBufferCheck = currentTime;
            
            // Check buffer for chunks that have expired
            var expiredChunks = new List<int2>();
            foreach (var inactive in _inactiveChunks)
            {
                // If chunk has been inactive too long
                if (currentTime - inactive.Value.timestamp > bufferTimeSeconds)
                {
                    expiredChunks.Add(inactive.Key);
                }
            }

            // Actually pool expired chunks
            foreach (var pos in expiredChunks)
            {
                if (_inactiveChunks.TryGetValue(pos, out var value))
                {
                    _chunkPool.Enqueue(value.gameObject);
                    _inactiveChunks.Remove(pos);
                }
            }
        }
        
        // Clean pending chunks.
        for (int i = _pendingChunks.Count - 1; i >= 0; i--)
        {
            if (!_visibleChunkPositions.Contains(_pendingChunks[i].position))
            {
                var chunk = _pendingChunks[i];
                chunk.jobHandle.Complete(); // Must complete before disposing.
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
        // Complete and dispose all pending meshes
        foreach (var pendingMesh in _pendingMeshes)
        {
            pendingMesh.jobHandle.Complete();
            pendingMesh.vertices.Dispose();
            pendingMesh.triangles.Dispose();
            pendingMesh.uvs.Dispose();
            pendingMesh.colors.Dispose();
            pendingMesh.meshCounts.Dispose();
            pendingMesh.heightMap.Dispose();
        }
        _pendingMeshes.Clear();

        // Complete any pending jobs
        foreach (var job in _pendingJobs)
        {
            job.Complete();
        }

        _blockDefs.Dispose();
        _activeChunks.Clear();
        _chunkDataCache.Clear();
        _heightMapCache.Clear();

        // Clean up inactive chunks
        foreach (var inactive in _inactiveChunks)
        {
            if (inactive.Value.gameObject != null)
                Destroy(inactive.Value.gameObject);
        }
        _inactiveChunks.Clear();

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