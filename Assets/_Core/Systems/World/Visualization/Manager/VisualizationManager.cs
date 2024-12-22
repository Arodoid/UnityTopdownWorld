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

    // Mesh generation pools
    private List<Vector3> vertices = new List<Vector3>();
    private List<int> triangles = new List<int>();
    private List<Vector2> uvs = new List<Vector2>();
    private List<Color> colors = new List<Color>();

    public void Initialize()
    {
        chunkParent = new GameObject("Chunks").transform;
        chunkParent.SetParent(transform);
        chunkObjects.Clear();
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

        // Generate mesh data for each block
        foreach (var blockData in chunk.GetAllBlocks())
        {
            Vector3Int localPos = blockData.Key;
            byte blockID = blockData.Value;
            BlockType block = BlockType.FromID(blockID);
            
            // Skip air blocks
            if (blockID == BlockType.Air.ID) continue;
            
            AddBlockToMesh(localPos, chunk);
        }

        // Create and return mesh
        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.colors = colors.ToArray();
        mesh.RecalculateNormals();
        return mesh;
    }

    private void AddBlockToMesh(Vector3Int localPos, ChunkData chunk)
    {
        byte blockID = chunk.GetBlock(localPos);
        BlockType block = BlockType.FromID(blockID);

        // Check each face to see if it's exposed
        for (int face = 0; face < CubeMeshData.FaceTriangles.Length; face++)
        {
            Vector3Int neighborPos = localPos + Vector3Int.RoundToInt(CubeMeshData.Normals[face]);

            // If the neighbor block is air, add this face
            if (chunk.GetBlock(neighborPos) == BlockType.Air.ID)
            {
                int vertexOffset = vertices.Count;
                
                // Add vertices for this face (4 vertices)
                for (int vert = 0; vert < 4; vert++)
                {
                    int vertexIndex = CubeMeshData.FaceTriangles[face][vert];
                    vertices.Add(CubeMeshData.Vertices[vertexIndex] + localPos);
                    uvs.Add(CubeMeshData.UVs[vert]);  // Add UV for each vertex
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
} 