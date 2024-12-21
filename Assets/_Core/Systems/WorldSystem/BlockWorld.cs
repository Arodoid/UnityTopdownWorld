using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BlockWorld : MonoBehaviour
{
    public static BlockWorld Instance { get; private set; }

    // Block type definitions
    public const byte AIR = 0;
    public const byte DIRT = 1;
    public const byte STONE = 2;
    public const byte GRASS = 3;

    public const int WORLD_RADIUS_CHUNKS = 500;  // 500 chunks in each direction = 1000x1000
    public const int WORLD_SIZE_BLOCKS = WORLD_RADIUS_CHUNKS * ChunkData.CHUNK_SIZE * 2;  // Total size in blocks

    private Dictionary<Vector2Int, ChunkData> chunks = new Dictionary<Vector2Int, ChunkData>();
    private Dictionary<Vector2Int, GameObject> chunkMeshes = new Dictionary<Vector2Int, GameObject>();
    private HashSet<Vector2Int> activeChunks = new HashSet<Vector2Int>();
    private HashSet<Vector2Int> dirtyChunks = new HashSet<Vector2Int>();
    
    [SerializeField] private int viewDistance = 5;  // In chunks
    private WorldGenerator worldGenerator;

    // Mesh generation pools
    private List<Vector3> vertexPool = new List<Vector3>();
    private List<int> trianglePool = new List<int>();
    private List<Color> colorPool = new List<Color>();
    private List<Vector3> normalPool = new List<Vector3>();

    [SerializeField] private Material blockMaterial; // Assign the new shader material

    private void Awake()
    {
        Instance = this;
        worldGenerator = GetComponent<WorldGenerator>();
        
        if (blockMaterial != null)
        {
            blockMaterial.SetVector("_LightDirection", Vector3.down); // Just straight down
        }
        
        Debug.Log($"BlockWorld initialized with view distance: {viewDistance} chunks");
    }

    private void Start()
    {
        // Generate initial chunks around origin
        Vector2Int currentChunkPos = new Vector2Int(0, 0);
        for (int x = -viewDistance; x <= viewDistance; x++)
        {
            for (int y = -viewDistance; y <= viewDistance; y++)
            {
                Vector2Int chunkPos = new Vector2Int(currentChunkPos.x + x, currentChunkPos.y + y);
                LoadChunk(chunkPos);
            }
        }
        
        // Update visible chunks
        UpdateVisibleChunks(currentChunkPos);

        // NEW CODE: Generate debug area around 0,0
        int debugSize = 10; // This will create 100x100 area (-50 to +50)
        for (int x = -debugSize; x <= debugSize; x++)
        {
            for (int y = -debugSize; y <= debugSize; y++)
            {
                Vector2Int debugChunkPos = new Vector2Int(x, y);
                LoadChunk(debugChunkPos);
                if (!chunkMeshes.ContainsKey(debugChunkPos))
                {
                    chunkMeshes[debugChunkPos] = CreateChunkMesh(debugChunkPos);
                }
            }
        }

        // Add debug visualization
        StartCoroutine(DrawDebugArea(debugSize));
    }

    private IEnumerator DrawDebugArea(int debugSize)
    {
        // World size in chunks (500 chunks in each direction from center)
        const int WORLD_RADIUS_CHUNKS = 500;  // This makes 1000x1000
        Color worldBoundaryColor = new Color(1f, 0f, 0f, 0.5f); // Semi-transparent red
        Color loadedAreaColor = new Color(0f, 1f, 0f, 0.5f);    // Semi-transparent green

        while (true)
        {
            // Draw current loaded debug area (green)
            float debugWorldSize = debugSize * ChunkData.CHUNK_SIZE;
            Debug.DrawLine(
                new Vector3(-debugWorldSize, 0, -debugWorldSize),
                new Vector3(debugWorldSize, 0, -debugWorldSize),
                loadedAreaColor
            );
            Debug.DrawLine(
                new Vector3(-debugWorldSize, 0, debugWorldSize),
                new Vector3(debugWorldSize, 0, debugWorldSize),
                loadedAreaColor
            );
            Debug.DrawLine(
                new Vector3(-debugWorldSize, 0, -debugWorldSize),
                new Vector3(-debugWorldSize, 0, debugWorldSize),
                loadedAreaColor
            );
            Debug.DrawLine(
                new Vector3(debugWorldSize, 0, -debugWorldSize),
                new Vector3(debugWorldSize, 0, debugWorldSize),
                loadedAreaColor
            );

            // Draw world boundaries (red)
            float worldSize = WORLD_RADIUS_CHUNKS * ChunkData.CHUNK_SIZE;
            Debug.DrawLine(
                new Vector3(-worldSize, 0, -worldSize),
                new Vector3(worldSize, 0, -worldSize),
                worldBoundaryColor
            );
            Debug.DrawLine(
                new Vector3(-worldSize, 0, worldSize),
                new Vector3(worldSize, 0, worldSize),
                worldBoundaryColor
            );
            Debug.DrawLine(
                new Vector3(-worldSize, 0, -worldSize),
                new Vector3(-worldSize, 0, worldSize),
                worldBoundaryColor
            );
            Debug.DrawLine(
                new Vector3(worldSize, 0, -worldSize),
                new Vector3(worldSize, 0, worldSize),
                worldBoundaryColor
            );

            // Optional: Draw text labels for scale reference
            Vector3 centerPos = Vector3.zero + Vector3.up * 10f; // Raise text slightly above ground
            Debug.DrawLine(Vector3.zero, Vector3.up * 10f, Color.white); // Center marker
            
            // Draw chunk grid lines (optional, but helps visualize scale)
            float gridInterval = ChunkData.CHUNK_SIZE * 50f; // Draw every 50 chunks
            for (float x = -worldSize; x <= worldSize; x += gridInterval)
            {
                Debug.DrawLine(
                    new Vector3(x, 0, -worldSize),
                    new Vector3(x, 0, worldSize),
                    new Color(0.5f, 0.5f, 0.5f, 0.2f)
                );
            }
            for (float z = -worldSize; z <= worldSize; z += gridInterval)
            {
                Debug.DrawLine(
                    new Vector3(-worldSize, 0, z),
                    new Vector3(worldSize, 0, z),
                    new Color(0.5f, 0.5f, 0.5f, 0.2f)
                );
            }

            yield return new WaitForSeconds(0.1f);
        }
    }

    public void SetBlock(Vector3Int worldPos, byte blockType)
    {
        // Convert world position to chunk coordinates
        Vector2Int chunkPos = new Vector2Int(
            Mathf.FloorToInt((float)worldPos.x / ChunkData.CHUNK_SIZE),
            Mathf.FloorToInt((float)worldPos.z / ChunkData.CHUNK_SIZE)
        );

        // Convert to local coordinates within the chunk
        Vector3Int localPos = new Vector3Int(
            ((worldPos.x % ChunkData.CHUNK_SIZE) + ChunkData.CHUNK_SIZE) % ChunkData.CHUNK_SIZE,
            worldPos.y,
            ((worldPos.z % ChunkData.CHUNK_SIZE) + ChunkData.CHUNK_SIZE) % ChunkData.CHUNK_SIZE
        );

        Debug.Log($"Setting block at world pos: {worldPos}, chunk pos: {chunkPos}, local pos: {localPos}");

        // Ensure the chunk exists
        if (!chunks.ContainsKey(chunkPos))
        {
            Debug.LogWarning($"Attempting to set block in non-existent chunk at {chunkPos}");
            return;
        }

        // Set the block
        chunks[chunkPos].SetBlock(localPos.x, localPos.y, localPos.z, blockType);

        // Regenerate the chunk mesh
        RegenerateChunkMesh(chunkPos);

        // Update neighboring chunks if the block is on a border
        if (localPos.x == 0)
            RegenerateChunkMesh(chunkPos + Vector2Int.left);
        if (localPos.x == ChunkData.CHUNK_SIZE - 1)
            RegenerateChunkMesh(chunkPos + Vector2Int.right);
        if (localPos.z == 0)
            RegenerateChunkMesh(chunkPos + Vector2Int.down);
        if (localPos.z == ChunkData.CHUNK_SIZE - 1)
            RegenerateChunkMesh(chunkPos + Vector2Int.up);
    }

    public byte GetBlock(Vector3Int worldPos)
    {
        // Convert world position to chunk coordinates
        Vector2Int chunkPos = new Vector2Int(
            Mathf.FloorToInt((float)worldPos.x / ChunkData.CHUNK_SIZE),
            Mathf.FloorToInt((float)worldPos.z / ChunkData.CHUNK_SIZE)
        );

        // Convert to local coordinates within the chunk
        Vector3Int localPos = new Vector3Int(
            ((worldPos.x % ChunkData.CHUNK_SIZE) + ChunkData.CHUNK_SIZE) % ChunkData.CHUNK_SIZE,
            worldPos.y,
            ((worldPos.z % ChunkData.CHUNK_SIZE) + ChunkData.CHUNK_SIZE) % ChunkData.CHUNK_SIZE
        );

        // Return air if chunk doesn't exist
        if (!chunks.ContainsKey(chunkPos))
        {
            return AIR;
        }

        return chunks[chunkPos].GetBlock(localPos.x, localPos.y, localPos.z);
    }

    private Vector2Int WorldToChunkPosition(Vector3Int worldPos)
    {
        return new Vector2Int(
            Mathf.FloorToInt(worldPos.x / (float)ChunkData.CHUNK_SIZE),
            Mathf.FloorToInt(worldPos.z / (float)ChunkData.CHUNK_SIZE)
        );
    }

    private Vector3Int WorldToLocalPosition(Vector3Int worldPos)
    {
        return new Vector3Int(
            ((worldPos.x % ChunkData.CHUNK_SIZE) + ChunkData.CHUNK_SIZE) % ChunkData.CHUNK_SIZE,
            ((worldPos.y % ChunkData.CHUNK_SIZE) + ChunkData.CHUNK_SIZE) % ChunkData.CHUNK_SIZE,
            worldPos.z
        );
    }

    public void UpdateVisibleChunks(Vector2Int currentChunkPos)
    {
        HashSet<Vector2Int> newActiveChunks = new HashSet<Vector2Int>();
        
        // Generate/Load chunks in view distance
        for (int x = -viewDistance; x <= viewDistance; x++)
        {
            for (int y = -viewDistance; y <= viewDistance; y++)
            {
                Vector2Int chunkPos = new Vector2Int(currentChunkPos.x + x, currentChunkPos.y + y);
                
                // Load chunk if it doesn't exist
                if (!chunks.ContainsKey(chunkPos))
                {
                    LoadChunk(chunkPos);
                }
                
                if (WorldManager.Instance.IsChunkVisible(chunkPos))
                {
                    newActiveChunks.Add(chunkPos);
                    
                    if (!activeChunks.Contains(chunkPos))
                    {
                        // Chunk just became visible
                        if (!chunkMeshes.ContainsKey(chunkPos))
                        {
                            chunkMeshes[chunkPos] = CreateChunkMesh(chunkPos);
                        }
                    }
                }
            }
        }
        
        // Remove chunks that are no longer visible
        foreach (var chunk in activeChunks)
        {
            if (!newActiveChunks.Contains(chunk))
            {
                UnloadChunkMesh(chunk);
            }
        }
        
        activeChunks = newActiveChunks;
    }

    private void LoadChunk(Vector2Int pos)
    {
        if (!chunks.ContainsKey(pos))
        {
            // Create chunk data for all vertical layers
            for (int z = 0; z < ChunkData.WORLD_DEPTH; z++)
            {
                Vector3Int chunkPos = new Vector3Int(pos.x, pos.y, z);
                ChunkData chunk = new ChunkData(chunkPos);
                chunks[pos] = chunk;
                worldGenerator.GenerateChunk(chunk);
            }
            
            // Create mesh if chunk is visible
            if (WorldManager.Instance.IsChunkVisible(pos))
            {
                chunkMeshes[pos] = CreateChunkMesh(pos);
            }
        }
    }

    private void UnloadChunkMesh(Vector2Int pos)
    {
        if (chunkMeshes.TryGetValue(pos, out GameObject meshObj))
        {
            Destroy(meshObj);
            chunkMeshes.Remove(pos);
        }
    }

    public void UpdateChunkMesh(Vector2Int pos)
    {
        if (!chunks.ContainsKey(pos))
        {
            Debug.LogWarning($"Trying to update mesh for non-existent chunk at {pos}");
            return;
        }

        Debug.Log($"Updating chunk mesh at {pos}");  // Debug log

        // If the chunk mesh exists, destroy it first
        if (chunkMeshes.TryGetValue(pos, out GameObject existingMesh))
        {
            DestroyImmediate(existingMesh);  // Use DestroyImmediate instead of Destroy
            chunkMeshes.Remove(pos);
        }

        // Create new mesh
        try 
        {
            GameObject newMesh = CreateChunkMesh(pos);
            chunkMeshes[pos] = newMesh;
            Debug.Log($"Successfully created new mesh for chunk at {pos}");  // Debug log
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to create chunk mesh at {pos}: {e}");
        }
    }

    private void RegenerateChunkMesh(Vector2Int pos)
    {
        if (chunks.ContainsKey(pos) && chunkMeshes.ContainsKey(pos))
        {
            GameObject oldMesh = chunkMeshes[pos];
            GameObject newMesh = CreateChunkMesh(pos);
            
            // Copy transform position
            newMesh.transform.position = oldMesh.transform.position;
            
            // Destroy old mesh
            DestroyImmediate(oldMesh);
            
            // Update dictionary
            chunkMeshes[pos] = newMesh;
        }
    }

    private void AddFace(Vector3[] faceVertices, Vector3Int localPos, Color color)
    {
        int vertexOffset = vertexPool.Count;
        
        // Get the normal for this face
        Vector3 normal = CubeMeshData.FaceNormals[faceVertices];
        
        // Add vertices
        for (int i = 0; i < faceVertices.Length; i++)
        {
            vertexPool.Add(faceVertices[i] + localPos);
            colorPool.Add(color);
            normalPool.Add(normal);
        }
        
        // Add triangles
        trianglePool.Add(vertexOffset + 0);
        trianglePool.Add(vertexOffset + 1);
        trianglePool.Add(vertexOffset + 2);
        trianglePool.Add(vertexOffset + 0);
        trianglePool.Add(vertexOffset + 2);
        trianglePool.Add(vertexOffset + 3);
    }

    private bool IsBlockFaceVisible(Vector2Int chunkPos, Vector3Int blockPos)
    {
        // If block is outside chunk bounds, check neighboring chunk
        Vector2Int targetChunkPos = chunkPos;
        Vector3Int localPos = blockPos;
        
        // Handle X direction
        if (blockPos.x >= ChunkData.CHUNK_SIZE)
        {
            targetChunkPos.x += 1;
            localPos.x = 0;
        }
        else if (blockPos.x < 0)
        {
            targetChunkPos.x -= 1;
            localPos.x = ChunkData.CHUNK_SIZE - 1;
        }
        
        // Handle Z direction (was Y before)
        if (blockPos.z >= ChunkData.CHUNK_SIZE)
        {
            targetChunkPos.y += 1;
            localPos.z = 0;
        }
        else if (blockPos.z < 0)
        {
            targetChunkPos.y -= 1;
            localPos.z = ChunkData.CHUNK_SIZE - 1;
        }

        // Handle Y direction (vertical)
        if (blockPos.y >= ChunkData.CHUNK_SIZE || blockPos.y < 0)
        {
            return true; // Always show faces at chunk boundaries
        }

        // Check if block at position is air
        if (!chunks.ContainsKey(targetChunkPos))
            return true;
            
        return chunks[targetChunkPos].GetBlock(localPos.x, localPos.y, localPos.z) == AIR;
    }

    private Color GetBlockColor(byte blockType)
    {
        switch (blockType)
        {
            case DIRT:
                return new Color(0.6f, 0.4f, 0.2f); // Brown
            case STONE:
                return new Color(0.5f, 0.5f, 0.5f); // Gray
            case GRASS:
                return new Color(0.3f, 0.9f, 0.2f); // Brighter green
            default:
                return Color.white;
        }
    }

    private GameObject CreateChunkMesh(Vector2Int chunkPos)
    {
        // Clear working pools for new mesh data
        vertexPool.Clear();
        trianglePool.Clear();
        colorPool.Clear();
        normalPool.Clear();

        ChunkData chunk = chunks[chunkPos];
        
        // Create GameObject with mesh components
        GameObject chunkObject = new GameObject($"Chunk_{chunkPos.x}_{chunkPos.y}");
        chunkObject.transform.parent = this.transform;
        
        // Position the chunk in XZ plane
        chunkObject.transform.position = new Vector3(
            chunkPos.x * ChunkData.CHUNK_SIZE,
            0,  // Y is up
            chunkPos.y * ChunkData.CHUNK_SIZE  // Z instead of Y
        );

        // Add components
        MeshFilter meshFilter = chunkObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = chunkObject.AddComponent<MeshRenderer>();
        
        // Create and configure material
        Material material = new Material(Shader.Find("Custom/TerrainShadowShader"));
        
        // Enable vertex colors in the shader
        material.EnableKeyword("_VERTEX_COLORS");
        material.SetColor("_BaseColor", Color.white);  // This must be white to show vertex colors properly
        
        // Optional: adjust these for different material looks
        material.SetFloat("_Smoothness", 0.0f);
        material.SetFloat("_Metallic", 0.0f);
        
        meshRenderer.material = material;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        meshRenderer.receiveShadows = true;

        // For each non-air block in the chunk
        foreach (var blockData in chunk.GetNonAirBlocks())
        {
            Vector3Int localPos = blockData.Key;
            byte blockType = blockData.Value;
            
            // Check each face
            // Top face
            if (IsBlockFaceVisible(chunkPos, localPos + Vector3Int.up))
            {
                AddFace(CubeMeshData.TopFace, localPos, GetBlockColor(blockType));
            }
            
            // Front face
            if (IsBlockFaceVisible(chunkPos, localPos + Vector3Int.forward))
            {
                AddFace(CubeMeshData.FrontFace, localPos, GetBlockColor(blockType));
            }
            
            // Back face
            if (IsBlockFaceVisible(chunkPos, localPos + Vector3Int.back))
            {
                AddFace(CubeMeshData.BackFace, localPos, GetBlockColor(blockType));
            }
            
            // Left face
            if (IsBlockFaceVisible(chunkPos, localPos + Vector3Int.left))
            {
                AddFace(CubeMeshData.LeftFace, localPos, GetBlockColor(blockType));
            }
            
            // Right face
            if (IsBlockFaceVisible(chunkPos, localPos + Vector3Int.right))
            {
                AddFace(CubeMeshData.RightFace, localPos, GetBlockColor(blockType));
            }
        }

        // Create and assign mesh
        Mesh mesh = new Mesh();
        mesh.vertices = vertexPool.ToArray();
        mesh.triangles = trianglePool.ToArray();
        mesh.colors = colorPool.ToArray();
        mesh.normals = normalPool.ToArray();
        mesh.RecalculateBounds();
        
        meshFilter.mesh = mesh;

        // Add these lines after creating the GameObject
        MeshCollider meshCollider = chunkObject.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = mesh;
        
        // Set the layer for block interactions
        chunkObject.layer = LayerMask.NameToLayer("Blocks");

        return chunkObject;
    }
} 