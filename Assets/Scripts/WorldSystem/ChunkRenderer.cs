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
        
        // Mark static since we never modify the mesh - we only replace it
        chunkGO.isStatic = true;
        
        var lodGroup = chunkGO.AddComponent<LODGroup>();
        
        // Configure LODGroup for orthographic top-down view
        lodGroup.size = Chunk.ChunkSize * 2f;
        lodGroup.fadeMode = LODFadeMode.None;
        lodGroup.localReferencePoint = new Vector3(Chunk.ChunkSize/2f, 0, Chunk.ChunkSize/2f);
        
        LOD[] lods = new LOD[2]; // Changed from 3 to 2 LOD levels
        
        var highLOD = CreateLODLevel(chunkGO, mesh, "High");
        var ultraLowLOD = CreateLODLevel(chunkGO, CreateSingleQuadMesh(mesh), "Ultra");

        // Adjusted thresholds for two LOD levels
        lods[0] = new LOD(0.1f, new[] { highLOD });     // >10% screen coverage
        lods[1] = new LOD(0.0f, new[] { ultraLowLOD }); // <10% screen coverage

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
        var newVerts = new List<Vector3>();
        var newColors = new List<Color32>();
        var newTris = new List<int>();

        // Create one quad for each grid cell
        for (int x = 0; x < gridSize; x++)
        for (int z = 0; z < gridSize; z++)
        {
            float minX = x * factor;
            float maxX = minX + factor;
            float minZ = z * factor;
            float maxZ = minZ + factor;

            // Find all vertices in this grid cell
            var cellVertices = new List<Vector3>();
            var cellColors = new List<Color32>();

            for (int i = 0; i < vertices.Length; i++)
            {
                if (i >= normals.Length || normals[i].y <= 0.5f) continue;

                var v = vertices[i];
                if (v.x >= minX && v.x < maxX && v.z >= minZ && v.z < maxZ)
                {
                    cellVertices.Add(v);
                    if (i < colors32.Length) cellColors.Add(colors32[i]);
                }
            }

            // Skip if no vertices in this cell
            if (cellVertices.Count == 0) continue;

            // Get maximum height and dominant color for this cell
            float cellHeight = cellVertices.Max(v => v.y);
            Color32 cellColor = GetDominantColor(cellColors.ToArray());

            int baseIndex = newVerts.Count;

            // Create quad vertices
            newVerts.Add(new Vector3(minX, cellHeight, minZ));
            newVerts.Add(new Vector3(maxX, cellHeight, minZ));
            newVerts.Add(new Vector3(minX, cellHeight, maxZ));
            newVerts.Add(new Vector3(maxX, cellHeight, maxZ));

            // Add colors
            for (int i = 0; i < 4; i++)
            {
                newColors.Add(cellColor);
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
        renderer.allowOcclusionWhenDynamic = true;
        
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

    void OnGUI()
    {
        var lodGroups = UnityEngine.Object.FindObjectsByType<LODGroup>(FindObjectsSortMode.None);
        int highLODCount = 0;
        int lowLODCount = 0;
        
        foreach (var lodGroup in lodGroups)
        {
            if (lodGroup.lodCount > 0)
            {
                // Calculate the current LOD level based on the camera's distance
                Camera mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    float distance = Vector3.Distance(mainCamera.transform.position, lodGroup.transform.position);
                    LOD[] lods = lodGroup.GetLODs();
                    for (int i = 0; i < lods.Length; i++)
                    {
                        if (distance < lods[i].screenRelativeTransitionHeight * mainCamera.pixelHeight)
                        {
                            if (i == 0) highLODCount++;
                            if (i == 1) lowLODCount++;
                            break;
                        }
                    }
                }
            }
        }
        
        GUI.Label(new Rect(10, 70, 200, 20), $"High LOD: {highLODCount}");
        GUI.Label(new Rect(10, 90, 200, 20), $"Low LOD: {lowLODCount}");
    }
}