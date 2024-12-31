using UnityEngine;
using System;
using System.Collections.Generic;
using VoxelGame.Utilities;

public class ChunkRenderer
{
    private readonly Dictionary<Vector3Int, GameObject> _renderedChunks = new Dictionary<Vector3Int, GameObject>();
    private readonly Dictionary<Vector3Int, Mesh> _chunkMeshes = new Dictionary<Vector3Int, Mesh>();
    private readonly ObjectPool<Mesh> _meshPool;

    public ChunkRenderer(ObjectPool<Mesh> meshPool)
    {
        _meshPool = meshPool ?? throw new ArgumentNullException(nameof(meshPool));
    }

    public void RenderChunk(Chunk chunk, Mesh mesh)
    {
        Vector3Int position = chunk.Position;
        Vector3 worldPos = position * Chunk.ChunkSize;

        if (_chunkMeshes.TryGetValue(position, out Mesh oldMesh))
        {
            _meshPool.Release(oldMesh);
        }
        
        _chunkMeshes[position] = mesh;

        if (_renderedChunks.TryGetValue(position, out var existingChunkGO))
        {
            existingChunkGO.GetComponent<MeshFilter>().mesh = mesh;
        }
        else
        {
            GameObject chunkGO = new GameObject($"Chunk_{position.x}_{position.y}_{position.z}");
            chunkGO.transform.position = worldPos;

            MeshFilter meshFilter = chunkGO.AddComponent<MeshFilter>();
            meshFilter.mesh = mesh;

            MeshRenderer meshRenderer = chunkGO.AddComponent<MeshRenderer>();
            meshRenderer.material = BlockRegistry.TerrainMaterial;

            _renderedChunks[position] = chunkGO;
        }
    }

    public void RemoveChunkRender(Vector3Int position)
    {
        if (_chunkMeshes.TryGetValue(position, out Mesh mesh))
        {
            _meshPool.Release(mesh);
            _chunkMeshes.Remove(position);
        }
        if (_renderedChunks.TryGetValue(position, out var chunkGO))
        {
            UnityEngine.Object.Destroy(chunkGO);
            _renderedChunks.Remove(position);
        }
    }

    public bool IsChunkRendered(Vector3Int position)
    {
        return _renderedChunks.ContainsKey(position);
    }

    public Dictionary<Vector3Int, GameObject> GetRenderedChunks()
    {
        return _renderedChunks;
    }

    public void RenderChunkPooled(Chunk chunk, Mesh mesh, ObjectPool<Mesh> meshPool)
    {
        if (_chunkMeshes.TryGetValue(chunk.Position, out Mesh oldMesh))
        {
            meshPool.Release(oldMesh);
        }
        
        _chunkMeshes[chunk.Position] = mesh;

        if (_renderedChunks.TryGetValue(chunk.Position, out var existingChunkGO))
        {
            existingChunkGO.GetComponent<MeshFilter>().mesh = mesh;
        }
        else
        {
            GameObject chunkGO = new GameObject($"Chunk_{chunk.Position.x}_{chunk.Position.y}_{chunk.Position.z}");
            chunkGO.transform.position = chunk.Position * Chunk.ChunkSize;

            MeshFilter meshFilter = chunkGO.AddComponent<MeshFilter>();
            meshFilter.mesh = mesh;

            MeshRenderer meshRenderer = chunkGO.AddComponent<MeshRenderer>();
            meshRenderer.material = BlockRegistry.TerrainMaterial;

            _renderedChunks[chunk.Position] = chunkGO;
        }
    }
}