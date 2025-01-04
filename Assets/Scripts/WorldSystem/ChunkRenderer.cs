using UnityEngine;
using System;
using System.Collections.Generic;
using VoxelGame.Utilities;
using VoxelGame.Interfaces;
using Object = UnityEngine.Object;

public class ChunkRenderer : MonoBehaviour, IChunkRenderer
{
    private readonly Dictionary<Vector3Int, GameObject> _renderedChunks = new Dictionary<Vector3Int, GameObject>();
    private readonly Dictionary<Vector3Int, Mesh> _chunkMeshes = new Dictionary<Vector3Int, Mesh>();
    private ObjectPool<Mesh> _meshPool;
    
    private void Awake()
    {
        _meshPool = new ObjectPool<Mesh>(
            new Func<Mesh>(() => new Mesh()),
            new Action<Mesh>(mesh => mesh.Clear()),
            new Action<Mesh>(mesh => mesh.Clear()),
            50
        );
    }

    public void RenderChunk(Chunk chunk, Mesh mesh)
    {
        if (!BlockRegistry.IsInitialized || BlockRegistry.TerrainMaterial == null)
        {
            Debug.LogWarning("BlockRegistry not initialized or TerrainMaterial is null");
            return;
        }

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

        if (_renderedChunks.TryGetValue(position, out GameObject chunkGO))
        {
            if (chunkGO != null)
            {
                Destroy(chunkGO);
            }
            _renderedChunks.Remove(position);
        }
    }

    public bool IsChunkRendered(Vector3Int position)
    {
        return _renderedChunks.ContainsKey(position);
    }

    private void OnDestroy()
    {
        foreach (var mesh in _chunkMeshes.Values)
        {
            if (mesh != null)
            {
                _meshPool.Release(mesh);
            }
        }
        
        foreach (var go in _renderedChunks.Values)
        {
            if (go != null)
            {
                Destroy(go);
            }
        }
        
        _chunkMeshes.Clear();
        _renderedChunks.Clear();
    }
}