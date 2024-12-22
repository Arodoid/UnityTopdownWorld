using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Handles visual representation of chunks.
/// Creates and manages meshes and GameObjects.
/// </summary>
public class VisualizationManager : MonoBehaviour, IWorldSystem
{
    [Header("Visualization Settings")]
    [SerializeField] private Material blockMaterial;
    
    private Dictionary<Vector3Int, GameObject> chunkObjects = new Dictionary<Vector3Int, GameObject>();
    private Transform chunkParent;

    // Pre-allocated buffers with initial capacity to reduce resizing
    private const int INITIAL_BUFFER_SIZE = 4096;
    private List<Vector3> vertices = new List<Vector3>(INITIAL_BUFFER_SIZE);
    private List<int> triangles = new List<int>(INITIAL_BUFFER_SIZE);
    private List<Vector2> uvs = new List<Vector2>(INITIAL_BUFFER_SIZE);
    private List<Color> colors = new List<Color>(INITIAL_BUFFER_SIZE);

    // Cache for neighbor checks to reduce GC
    private static readonly Vector3Int[] neighborOffsets = new Vector3Int[6];
    private bool[,,] visibleFaces;

    public void Initialize()
    {
        chunkParent = new GameObject("Chunks").transform;
        chunkParent.SetParent(transform);
        chunkObjects.Clear();
        
        // Cache neighbor check directions
        for (int i = 0; i < 6; i++)
        {
            neighborOffsets[i] = Vector3Int.RoundToInt(CubeMeshData.Normals[i]);
        }
        
        // Initialize visibility cache
        visibleFaces = new bool[ChunkData.CHUNK_SIZE, ChunkData.CHUNK_SIZE, ChunkData.CHUNK_SIZE];
    }

    public void Cleanup()
    {
        foreach (var obj in chunkObjects.Values)
        {
            if (obj != null)
            {
                Destroy(obj);
            }
        }
        chunkObjects.Clear();
    }

    public void CreateChunkMesh(ChunkData chunk)
    {
        Vector3Int chunkPos = chunk.Position;

        // If chunk mesh already exists, update it instead
        if (chunkObjects.ContainsKey(chunkPos))
        {
            UpdateChunkMesh(chunk);
            return;
        }

        // Create chunk GameObject
        GameObject chunkObject = new GameObject($"Chunk {chunkPos.x}, {chunkPos.y}, {chunkPos.z}");
        chunkObject.transform.SetParent(chunkParent);
        chunkObject.transform.position = new Vector3(
            chunk.Position.x * ChunkData.CHUNK_SIZE,
            chunk.Position.y * ChunkData.CHUNK_SIZE,
            chunk.Position.z * ChunkData.CHUNK_SIZE
        );

        // Add mesh components
        MeshFilter meshFilter = chunkObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = chunkObject.AddComponent<MeshRenderer>();
        meshRenderer.material = blockMaterial;

        // Generate mesh
        Mesh mesh = GenerateChunkMesh(chunk);
        meshFilter.mesh = mesh;

        chunkObjects[chunkPos] = chunkObject;
    }

    public void UpdateChunkMesh(ChunkData chunk)
    {
        Vector3Int chunkPos = chunk.Position;
        if (chunkObjects.TryGetValue(chunkPos, out GameObject chunkObject))
        {
            MeshFilter meshFilter = chunkObject.GetComponent<MeshFilter>();
            if (meshFilter != null)
            {
                meshFilter.mesh = GenerateChunkMesh(chunk);
            }
        }
    }

    public void RemoveChunkMesh(Vector3Int chunkPos)
    {
        if (chunkObjects.TryGetValue(chunkPos, out GameObject chunkObject))
        {
            Destroy(chunkObject);
            chunkObjects.Remove(chunkPos);
        }
    }

    private Mesh GenerateChunkMesh(ChunkData chunk)
    {
        vertices.Clear();
        triangles.Clear();
        uvs.Clear();
        colors.Clear();

        // Pre-calculate visible faces
        PrecomputeVisibleFaces(chunk);

        // Generate mesh data for each block
        for (int x = 0; x < ChunkData.CHUNK_SIZE; x++)
        {
            for (int y = 0; y < ChunkData.CHUNK_SIZE; y++)
            {
                for (int z = 0; z < ChunkData.CHUNK_SIZE; z++)
                {
                    Vector3Int localPos = new Vector3Int(x, y, z);
                    byte blockID = chunk.GetBlock(localPos);
                    
                    // Skip air blocks
                    if (blockID == BlockType.Air.ID) continue;
                    
                    AddBlockToMesh(localPos, blockID, chunk);
                }
            }
        }

        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // Support larger meshes
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.colors = colors.ToArray();
        
        // Optimize mesh
        mesh.Optimize();
        mesh.RecalculateNormals();
        return mesh;
    }

    private void PrecomputeVisibleFaces(ChunkData chunk)
    {
        for (int x = 0; x < ChunkData.CHUNK_SIZE; x++)
        {
            for (int y = 0; y < ChunkData.CHUNK_SIZE; y++)
            {
                for (int z = 0; z < ChunkData.CHUNK_SIZE; z++)
                {
                    visibleFaces[x, y, z] = false;
                    
                    Vector3Int pos = new Vector3Int(x, y, z);
                    byte blockID = chunk.GetBlock(pos);
                    
                    if (blockID != BlockType.Air.ID)
                    {
                        // Check each face
                        for (int face = 0; face < 6; face++)
                        {
                            Vector3Int neighborPos = pos + neighborOffsets[face];
                            if (IsAirBlock(chunk, neighborPos))
                            {
                                visibleFaces[x, y, z] = true;
                                break; // If any face is visible, we can stop checking
                            }
                        }
                    }
                }
            }
        }
    }

    private void AddBlockToMesh(Vector3Int localPos, byte blockID, ChunkData chunk)
    {
        // Skip if block has no visible faces
        if (!visibleFaces[localPos.x, localPos.y, localPos.z])
            return;

        BlockType block = BlockType.FromID(blockID);
        Vector3 offset = localPos;

        // Check each face
        for (int face = 0; face < 6; face++)
        {
            Vector3Int neighborPos = localPos + neighborOffsets[face];
            
            if (IsAirBlock(chunk, neighborPos))
            {
                int vertexOffset = vertices.Count;
                
                // Add vertices for this face (4 vertices)
                var faceTriangles = CubeMeshData.FaceTriangles[face];
                for (int vert = 0; vert < 4; vert++)
                {
                    vertices.Add(CubeMeshData.Vertices[faceTriangles[vert]] + offset);
                    uvs.Add(CubeMeshData.UVs[vert]);
                    colors.Add(block.Color);
                }

                // Add triangles
                triangles.Add(vertexOffset);
                triangles.Add(vertexOffset + 1);
                triangles.Add(vertexOffset + 2);
                triangles.Add(vertexOffset);
                triangles.Add(vertexOffset + 2);
                triangles.Add(vertexOffset + 3);
            }
        }
    }

    private bool IsAirBlock(ChunkData chunk, Vector3Int pos)
    {
        if (pos.x < 0 || pos.x >= ChunkData.CHUNK_SIZE ||
            pos.y < 0 || pos.y >= ChunkData.CHUNK_SIZE ||
            pos.z < 0 || pos.z >= ChunkData.CHUNK_SIZE)
        {
            return true; // Treat out-of-bounds as air
        }
        return chunk.GetBlock(pos) == BlockType.Air.ID;
    }

    public bool IsChunkVisualized(Vector3Int chunkPos)
    {
        return chunkObjects.ContainsKey(chunkPos) && chunkObjects[chunkPos] != null;
    }
} 