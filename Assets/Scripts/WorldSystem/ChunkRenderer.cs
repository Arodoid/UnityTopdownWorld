using UnityEngine;
using System;
using System.Collections.Generic;
using VoxelGame.Utilities;
using VoxelGame.Interfaces;
using System.Linq;

public class ChunkRenderer : MonoBehaviour, IChunkRenderer
{
    private readonly Dictionary<Vector3Int, GameObject> _renderedChunks = new Dictionary<Vector3Int, GameObject>();
    private readonly Dictionary<Vector3Int, Mesh> _chunkMeshes = new Dictionary<Vector3Int, Mesh>();
    private ObjectPool<Mesh> _meshPool;
    
    private void Awake()
    {
        _meshPool = new ObjectPool<Mesh>(
            () => new Mesh(),
            mesh => mesh.Clear(),
            mesh => mesh.Clear(),
            50
        );
    }

    public void RenderChunk(Chunk chunk, Mesh mesh)
    {
        if (chunk == null || mesh == null || !BlockRegistry.IsInitialized || BlockRegistry.TerrainMaterial == null)
            return;

        Vector3Int chunkPos = chunk.Position;
        Vector3 worldPos = new Vector3(
            chunkPos.x * Chunk.ChunkSize,
            chunkPos.y * Chunk.ChunkSize,
            chunkPos.z * Chunk.ChunkSize
        );

        if (_chunkMeshes.TryGetValue(chunkPos, out Mesh oldMesh))
        {
            _meshPool.Release(oldMesh);
        }
        _chunkMeshes[chunkPos] = mesh;

        if (_renderedChunks.TryGetValue(chunkPos, out var existingChunkGO))
        {
            UpdateExistingChunk(existingChunkGO, mesh);
        }
        else
        {
            CreateNewChunk(chunkPos, worldPos, mesh);
        }
    }

    private void UpdateExistingChunk(GameObject chunkGO, Mesh mesh)
    {
        var meshFilter = chunkGO.GetComponent<MeshFilter>() ?? chunkGO.AddComponent<MeshFilter>();
        meshFilter.mesh = mesh;
    }

    private void CreateNewChunk(Vector3Int chunkPos, Vector3 worldPos, Mesh mesh)
    {
        var chunkGO = new GameObject($"Chunk_{chunkPos.x}_{chunkPos.y}_{chunkPos.z}");
        chunkGO.transform.SetPositionAndRotation(worldPos, Quaternion.identity);
        chunkGO.transform.parent = transform;

        var lodGroup = chunkGO.AddComponent<LODGroup>();
        
        // Configure LODGroup for orthographic top-down view
        lodGroup.size = Chunk.ChunkSize * 2f;  // Increased size for better LOD transitions
        lodGroup.fadeMode = LODFadeMode.None;
        lodGroup.localReferencePoint = new Vector3(Chunk.ChunkSize/2f, 0, Chunk.ChunkSize/2f);
        
        LOD[] lods = new LOD[4]; // Now 4 LOD levels
        
        var highLOD = CreateLODLevel(chunkGO, mesh, "High");
        var medLOD = CreateLODLevel(chunkGO, SimplifyMeshForTopDown(mesh, 2), "Med");
        var lowLOD = CreateLODLevel(chunkGO, SimplifyMeshForTopDown(mesh, 4), "Low");
        var ultraLowLOD = CreateLODLevel(chunkGO, CreateSingleQuadMesh(mesh), "Ultra"); // New ultra-low LOD

        // Adjusted thresholds for screen coverage only
        lods[0] = new LOD(0.3f, new[] { highLOD });    // >30% screen coverage
        lods[1] = new LOD(0.15f, new[] { medLOD });    // 15-30% screen coverage
        lods[2] = new LOD(0.05f, new[] { lowLOD });    // 5-15% screen coverage
        lods[3] = new LOD(0.0f, new[] { ultraLowLOD }); // <5% screen coverage (never cull)

        lodGroup.SetLODs(lods);
        lodGroup.RecalculateBounds();

        _renderedChunks[chunkPos] = chunkGO;
    }

    private Color32 GetDominantColor(Color32[] colors)
    {
        if (colors == null || colors.Length == 0)
        {
            return new Color32(255, 255, 255, 255);
        }

        var colorCounts = new Dictionary<Color32, int>();
        
        foreach (var color in colors)
        {
            if (!colorCounts.ContainsKey(color))
                colorCounts[color] = 0;
            colorCounts[color]++;
        }

        Color32 dominantColor = colors[0];
        int maxCount = 0;
        
        foreach (var kvp in colorCounts)
        {
            if (kvp.Value > maxCount)
            {
                maxCount = kvp.Value;
                dominantColor = kvp.Key;
            }
        }

        return dominantColor;
    }

    private Mesh SimplifyMeshForTopDown(Mesh originalMesh, int factor)
    {
        var simplifiedMesh = new Mesh();
        var vertices = originalMesh.vertices;
        var colors32 = originalMesh.colors32;
        var normals = originalMesh.normals;

        if (vertices.Length == 0 || colors32.Length == 0) return originalMesh;

        var gridSize = Chunk.ChunkSize / factor;
        var vertexGrid = new Dictionary<Vector2Int, List<(float height, Color32 color, Vector3 normal)>>();

        // First pass: collect only upward-facing vertices
        for (int i = 0; i < vertices.Length; i++)
        {
            // Skip if not an upward-facing vertex (using normal)
            if (i < normals.Length && normals[i].y <= 0.5f) continue;

            var v = vertices[i];
            var gridPos = new Vector2Int(
                Mathf.FloorToInt(v.x / factor),
                Mathf.FloorToInt(v.z / factor)
            );

            if (!vertexGrid.ContainsKey(gridPos))
            {
                vertexGrid[gridPos] = new List<(float, Color32, Vector3)>();
            }

            if (i < colors32.Length)
            {
                vertexGrid[gridPos].Add((v.y, colors32[i], normals[i]));
            }
        }

        // Generate simplified mesh
        var newVerts = new List<Vector3>();
        var newColors = new List<Color32>();
        var newTris = new List<int>();

        for (int x = 0; x < gridSize; x++)
        for (int z = 0; z < gridSize; z++)
        {
            var pos = new Vector2Int(x, z);
            if (!vertexGrid.ContainsKey(pos) || vertexGrid[pos].Count == 0) continue;

            var cellData = vertexGrid[pos];
            // Take the highest point for this cell
            var maxY = cellData.Max(d => d.height);
            
            // Get the color from the highest point
            var topColor = cellData
                .Where(d => Mathf.Approximately(d.height, maxY))
                .Select(d => d.color)
                .FirstOrDefault();

            int baseIndex = newVerts.Count;
            float worldX = x * factor;
            float worldZ = z * factor;

            // Add vertices for this cell
            newVerts.Add(new Vector3(worldX, maxY, worldZ));
            newVerts.Add(new Vector3(worldX + factor, maxY, worldZ));
            newVerts.Add(new Vector3(worldX, maxY, worldZ + factor));
            newVerts.Add(new Vector3(worldX + factor, maxY, worldZ + factor));

            // Add colors
            for (int i = 0; i < 4; i++)
            {
                newColors.Add(topColor);
            }

            // Add triangles
            newTris.AddRange(new[] {
                baseIndex, baseIndex + 2, baseIndex + 1,
                baseIndex + 1, baseIndex + 2, baseIndex + 3
            });
        }

        simplifiedMesh.SetVertices(newVerts);
        simplifiedMesh.SetTriangles(newTris, 0);
        simplifiedMesh.SetColors(newColors);
        simplifiedMesh.RecalculateNormals();
        simplifiedMesh.RecalculateBounds();

        return simplifiedMesh;
    }

    private MeshRenderer CreateLODLevel(GameObject parent, Mesh mesh, string lodName)
    {
        var lodObj = new GameObject($"LOD_{lodName}");
        lodObj.transform.parent = parent.transform;
        lodObj.transform.localPosition = Vector3.zero;
        lodObj.transform.localScale = Vector3.one;

        var meshFilter = lodObj.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;

        var renderer = lodObj.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = BlockRegistry.TerrainMaterial;

        return renderer;
    }

    public void RemoveChunkRender(Vector3Int position)
    {
        if (_chunkMeshes.TryGetValue(position, out Mesh mesh))
        {
            _meshPool.Release(mesh);
            _chunkMeshes.Remove(position);
        }

        if (_renderedChunks.TryGetValue(position, out GameObject chunkGO))
        {
            if (chunkGO != null)
            {
                Destroy(chunkGO);
            }
            _renderedChunks.Remove(position);
        }
    }

    public bool IsChunkRendered(Vector3Int position) => _renderedChunks.ContainsKey(position);

    private void OnDestroy()
    {
        foreach (var mesh in _chunkMeshes.Values)
        {
            if (mesh != null) _meshPool.Release(mesh);
        }
        
        foreach (var go in _renderedChunks.Values)
        {
            if (go != null) Destroy(go);
        }
        
        _chunkMeshes.Clear();
        _renderedChunks.Clear();
    }

    private Mesh CreateSingleQuadMesh(Mesh originalMesh)
    {
        var simplifiedMesh = new Mesh();
        var vertices = originalMesh.vertices;
        var colors32 = originalMesh.colors32;

        if (vertices.Length == 0 || colors32.Length == 0) return originalMesh;

        // Calculate average height
        float avgHeight = vertices.Select(v => v.y).Average();
        
        // Get the most common color
        var dominantColor = GetDominantColor(colors32);

        // Create vertices for single quad
        var newVerts = new Vector3[]
        {
            new Vector3(0, avgHeight, 0),
            new Vector3(Chunk.ChunkSize, avgHeight, 0),
            new Vector3(0, avgHeight, Chunk.ChunkSize),
            new Vector3(Chunk.ChunkSize, avgHeight, Chunk.ChunkSize)
        };

        var newTris = new int[]
        {
            0, 2, 1,
            1, 2, 3
        };

        var newColors = new Color32[] 
        { 
            dominantColor, dominantColor, dominantColor, dominantColor 
        };

        simplifiedMesh.SetVertices(newVerts);
        simplifiedMesh.SetTriangles(newTris, 0);
        simplifiedMesh.SetColors(newColors);
        simplifiedMesh.RecalculateNormals();
        simplifiedMesh.RecalculateBounds();

        return simplifiedMesh;
    }
}