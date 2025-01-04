using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;
using VoxelGame.Interfaces;

public class ChunkMeshGenerator : MonoBehaviour, IChunkMeshGenerator 
{
    private readonly int chunkSize = Chunk.ChunkSize;
    private ChunkManager chunkManager;

    // Pooled collections with pre-allocated capacity
    private List<Vector3> verticesPool;
    private List<int> trianglesPool;
    private List<Color32> colorsPool;
    private List<Vector2> uvsPool;

    // Pre-calculated capacities
    private const int MAX_VERTICES_PER_CHUNK = 16 * 16 * 16 * 24;  // 16³ blocks * 24 vertices max per block
    private const int MAX_TRIANGLES_PER_CHUNK = 16 * 16 * 16 * 36; // 16³ blocks * 36 indices max per block
    private const int MAX_UVS_PER_CHUNK = MAX_VERTICES_PER_CHUNK;
    private const int MAX_COLORS_PER_CHUNK = MAX_VERTICES_PER_CHUNK;

    // Face checking optimization
    private readonly bool[] shouldRenderFace = new bool[5];
    private readonly int[] neighborX = new int[5];
    private readonly int[] neighborY = new int[5];
    private readonly int[] neighborZ = new int[5];
    
    // Neighbor chunk caching
    private readonly Chunk[] neighborCache = new Chunk[5];
    private Vector3Int currentChunkPos;
    private readonly Vector2[] rotatedUVsCache = new Vector2[4];

    // Profiler markers
    private static readonly ProfilerMarker GenerateMeshMarker = new ProfilerMarker("ChunkMeshGenerator.GenerateMeshPooled");
    private static readonly ProfilerMarker FaceCheckMarker = new ProfilerMarker("ChunkMeshGenerator.CheckFaces");
    private static readonly ProfilerMarker MeshBuildMarker = new ProfilerMarker("ChunkMeshGenerator.BuildMesh");

    private void Awake()
    {
        // Get ChunkManager reference
        chunkManager = GetComponent<ChunkManager>();
        if (chunkManager == null)
        {
            Debug.LogError("ChunkManager not found on the same GameObject!");
            enabled = false;
            return;
        }

        // Initialize pools with maximum possible sizes
        verticesPool = new List<Vector3>(MAX_VERTICES_PER_CHUNK);
        trianglesPool = new List<int>(MAX_TRIANGLES_PER_CHUNK);
        colorsPool = new List<Color32>(MAX_COLORS_PER_CHUNK);
        uvsPool = new List<Vector2>(MAX_UVS_PER_CHUNK);
    }

    public void GenerateMeshData(Chunk chunk, Mesh mesh, int yLimit)
    {
        if (verticesPool == null)
        {
            Debug.LogError("ChunkMeshGenerator not properly initialized!");
            return;
        }
        GenerateMeshPooled(chunk, mesh, yLimit);
    }

    public void GenerateMeshPooled(Chunk chunk, Mesh mesh, int yLimit)
    {
        using (GenerateMeshMarker.Auto())
        {
            // Clear pools
            verticesPool.Clear();
            trianglesPool.Clear();
            colorsPool.Clear();
            uvsPool.Clear();

            if (chunk.IsFullyEmpty())
            {
                mesh.Clear();
                return;
            }

            // Update neighbor cache if chunk changed
            if (currentChunkPos != chunk.Position)
            {
                UpdateNeighborCache(chunk);
                currentChunkPos = chunk.Position;
            }

            int chunkYOffset = chunk.Position.y * chunkSize;
            int effectiveYLimit = Mathf.Min(chunkSize, Mathf.Max(0, yLimit - chunkYOffset));

            // Generate mesh data
            using (MeshBuildMarker.Auto())
            {
                for (int y = 0; y < effectiveYLimit; y++)
                for (int x = 0; x < chunkSize; x++)
                for (int z = 0; z < chunkSize; z++)
                {
                    Block block = chunk.GetBlock(x, y, z);
                    if (block != null)
                    {
                        ProcessBlock(chunk, block, x, y, z, y == effectiveYLimit - 1);
                    }
                }
            }

            // Apply mesh data
            mesh.Clear();
            mesh.SetVertices(verticesPool);
            mesh.SetTriangles(trianglesPool, 0);
            mesh.SetColors(colorsPool);
            mesh.SetUVs(0, uvsPool);
            mesh.RecalculateNormals();
        }
    }

    private void ProcessBlock(Chunk chunk, Block block, int x, int y, int z, bool isAtYLimit)
    {
        switch (block.RenderType)
        {
            case BlockRenderType.Cube:
                using (FaceCheckMarker.Auto())
                {
                    CheckAllFaces(chunk, x, y, z, isAtYLimit);
                    AddCubeMesh(block, x, y, z);
                }
                break;
            case BlockRenderType.Flat:
                if (!isAtYLimit)
                {
                    AddFlatMesh(block, x, y, z);
                }
                break;
        }
    }

    private void CheckAllFaces(Chunk chunk, int x, int y, int z, bool isAtYLimit)
    {
        // Pre-calculate all neighbor coordinates
        for (int face = 0; face < 5; face++)
        {
            neighborX[face] = x + BlockFaceNormals[face].x;
            neighborY[face] = y + BlockFaceNormals[face].y;
            neighborZ[face] = z + BlockFaceNormals[face].z;
        }

        // Check all faces
        for (int face = 0; face < 5; face++)
        {
            bool isTopFace = face == 2;
            if (isAtYLimit && isTopFace)
            {
                shouldRenderFace[face] = true;
                continue;
            }

            int nx = neighborX[face];
            int ny = neighborY[face];
            int nz = neighborZ[face];

            if ((uint)nx < chunkSize && (uint)ny < chunkSize && (uint)nz < chunkSize)
            {
                // Fast path: within same chunk
                shouldRenderFace[face] = !IsOpaqueBlock(chunk, nx, ny, nz);
            }
            else
            {
                // Slow path: check neighbor chunks
                shouldRenderFace[face] = !IsOpaqueBlockInNeighbor(chunk, nx, ny, nz, face);
            }
        }
    }

    private bool IsOpaqueBlock(Chunk chunk, int x, int y, int z)
    {
        Block block = chunk.GetBlock(x, y, z);
        return block != null && block.IsOpaque;
    }

    private bool IsOpaqueBlockInNeighbor(Chunk chunk, int x, int y, int z, int face)
    {
        Chunk neighborChunk = neighborCache[face];
        if (neighborChunk == null) return false;

        // Wrap coordinates to chunk space
        x = x & 15; // equivalent to x % 16
        y = y & 15;
        z = z & 15;

        Block block = neighborChunk.GetBlock(x, y, z);
        return block != null && block.IsOpaque;
    }

    private void AddCubeMesh(Block block, int x, int y, int z)
    {
        Vector2[] blockUVs = block.UVs ?? DefaultUVs;
        Color32 blockColor = block.Color;
        int worldX = currentChunkPos.x * chunkSize + x;
        int worldY = currentChunkPos.y * chunkSize + y;
        int worldZ = currentChunkPos.z * chunkSize + z;

        for (int face = 0; face < 5; face++)
        {
            if (!shouldRenderFace[face]) continue;

            int vertexIndex = verticesPool.Count;
            Vector3[] faceVerts = BlockFaceVertices[face];

            // Add vertices
            for (int v = 0; v < 4; v++)
            {
                verticesPool.Add(new Vector3(
                    x + faceVerts[v].x,
                    y + faceVerts[v].y,
                    z + faceVerts[v].z
                ));
            }

            // Add triangles
            trianglesPool.Add(vertexIndex);
            trianglesPool.Add(vertexIndex + 1);
            trianglesPool.Add(vertexIndex + 2);
            trianglesPool.Add(vertexIndex + 2);
            trianglesPool.Add(vertexIndex + 3);
            trianglesPool.Add(vertexIndex);

            // Add colors
            for (int c = 0; c < 4; c++)
            {
                colorsPool.Add(blockColor);
            }

            // Add UVs with rotation
            int rotation = GetDeterministicRotation(worldX, worldY, worldZ, face);
            GetRotatedUVs(blockUVs, rotation, rotatedUVsCache);
            for (int u = 0; u < 4; u++)
            {
                uvsPool.Add(rotatedUVsCache[u]);
            }
        }
    }

    private void UpdateNeighborCache(Chunk chunk)
    {
        for (int i = 0; i < 5; i++)
        {
            Vector3Int neighborPos = new Vector3Int(
                chunk.Position.x + BlockFaceNormals[i].x,
                chunk.Position.y + BlockFaceNormals[i].y,
                chunk.Position.z + BlockFaceNormals[i].z
            );
            neighborCache[i] = chunkManager.GetChunk(neighborPos);
        }
    }

    private void AddFlatMesh(Block block, int x, int y, int z)
    {
        int vertexIndex = verticesPool.Count;
        float height = 1f;

        // Add vertices for cross mesh
        verticesPool.Add(new Vector3(x, y, z));
        verticesPool.Add(new Vector3(x + 1, y, z));
        verticesPool.Add(new Vector3(x + 1, y + height, z));
        verticesPool.Add(new Vector3(x, y + height, z));

        // Add UVs
        Vector2[] blockUVs = block.UVs ?? DefaultUVs;
        for (int i = 0; i < 4; i++)
        {
            uvsPool.Add(blockUVs[i]);
        }

        // Add colors
        Color32 color = block.Color;
        for (int i = 0; i < 4; i++)
        {
            colorsPool.Add(color);
        }

        // Add triangles
        trianglesPool.Add(vertexIndex);
        trianglesPool.Add(vertexIndex + 1);
        trianglesPool.Add(vertexIndex + 2);
        trianglesPool.Add(vertexIndex + 2);
        trianglesPool.Add(vertexIndex + 3);
        trianglesPool.Add(vertexIndex);
    }

    private static readonly Vector3Int[] BlockFaceNormals = {
        new Vector3Int(0, 0, 1),  // Front
        new Vector3Int(0, 0, -1), // Back
        new Vector3Int(0, 1, 0),  // Top
        new Vector3Int(1, 0, 0),  // Right
        new Vector3Int(-1, 0, 0)  // Left
    };

    private static readonly Vector3[][] BlockFaceVertices = {
        // Front
        new [] { new Vector3(0,0,1), new Vector3(1,0,1), new Vector3(1,1,1), new Vector3(0,1,1) },
        // Back
        new [] { new Vector3(1,0,0), new Vector3(0,0,0), new Vector3(0,1,0), new Vector3(1,1,0) },
        // Top
        new [] { new Vector3(0,1,0), new Vector3(0,1,1), new Vector3(1,1,1), new Vector3(1,1,0) },
        // Right
        new [] { new Vector3(1,0,1), new Vector3(1,0,0), new Vector3(1,1,0), new Vector3(1,1,1) },
        // Left
        new [] { new Vector3(0,0,0), new Vector3(0,0,1), new Vector3(0,1,1), new Vector3(0,1,0) }
    };

    private static readonly Vector2[] DefaultUVs = {
        new Vector2(0, 0),
        new Vector2(1, 0),
        new Vector2(1, 1),
        new Vector2(0, 1)
    };

    private void GetRotatedUVs(Vector2[] originalUVs, int rotations, Vector2[] result)
    {
        for (int i = 0; i < 4; i++)
        {
            result[i] = originalUVs[(i + rotations) % 4];
        }
    }

    private int GetDeterministicRotation(int x, int y, int z, int face)
    {
        int hash = x * 73856093 ^ y * 19349663 ^ z * 83492791 ^ face;
        return (hash & 0x7fffffff) % 4;
    }
}