using UnityEngine;
using System;
using System.Collections.Generic;
using VoxelGame.Utilities;
using Object = UnityEngine.Object;

public class ChunkRenderer
{
    private readonly Dictionary<Vector3Int, GameObject> _renderedChunks = new Dictionary<Vector3Int, GameObject>();
    private readonly Dictionary<Vector3Int, Mesh> _chunkMeshes = new Dictionary<Vector3Int, Mesh>();
    private readonly Dictionary<Vector3Int, MaterialPropertyBlock> _propertyBlocks = new Dictionary<Vector3Int, MaterialPropertyBlock>();
    private readonly ObjectPool<Mesh> _meshPool;
    private readonly MonoBehaviour _coroutineRunner;
    
    private const float FADE_DURATION = 0.5f;
    private static readonly int OpacityProperty = Shader.PropertyToID("_Opacity");

    public ChunkRenderer(ObjectPool<Mesh> meshPool, MonoBehaviour coroutineRunner)
    {
        _meshPool = meshPool ?? throw new ArgumentNullException(nameof(meshPool));
        _coroutineRunner = coroutineRunner ?? throw new ArgumentNullException(nameof(coroutineRunner));
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
        if (_renderedChunks.TryGetValue(position, out var chunkGO))
        {
            _coroutineRunner.StartCoroutine(FadeOut(position));
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
        if (!BlockRegistry.IsInitialized || BlockRegistry.TerrainMaterial == null)
        {
            Debug.LogError("Cannot render chunk: BlockRegistry not properly initialized!");
            return;
        }

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
            Material material = BlockRegistry.TerrainMaterial;
            
            if (material.mainTexture == null)
            {
                Debug.LogError("Terrain material has no texture assigned!");
                material.color = Color.magenta;
            }
            
            meshRenderer.material = material;

            var propertyBlock = new MaterialPropertyBlock();
            propertyBlock.SetFloat(OpacityProperty, 0f);
            meshRenderer.SetPropertyBlock(propertyBlock);
            _propertyBlocks[chunk.Position] = propertyBlock;

            _renderedChunks[chunk.Position] = chunkGO;
            
            _coroutineRunner.StartCoroutine(FadeIn(chunk.Position));
        }
    }

    private System.Collections.IEnumerator FadeIn(Vector3Int position)
    {
        if (!_renderedChunks.TryGetValue(position, out var chunkGO)) yield break;
        
        var meshRenderer = chunkGO.GetComponent<MeshRenderer>();
        var propertyBlock = _propertyBlocks[position];
        float elapsed = 0f;

        while (elapsed < FADE_DURATION)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(0f, 1f, elapsed / FADE_DURATION);
            
            propertyBlock.SetFloat(OpacityProperty, alpha);
            meshRenderer.SetPropertyBlock(propertyBlock);
            
            yield return null;
        }

        propertyBlock.SetFloat(OpacityProperty, 1f);
        meshRenderer.SetPropertyBlock(propertyBlock);
    }

    private System.Collections.IEnumerator FadeOut(Vector3Int position)
    {
        if (!_renderedChunks.TryGetValue(position, out var chunkGO)) yield break;
        
        var meshRenderer = chunkGO.GetComponent<MeshRenderer>();
        var propertyBlock = _propertyBlocks[position];
        float elapsed = 0f;

        while (elapsed < FADE_DURATION)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / FADE_DURATION);
            
            propertyBlock.SetFloat(OpacityProperty, alpha);
            meshRenderer.SetPropertyBlock(propertyBlock);
            
            yield return null;
        }

        CleanupChunk(position);
    }

    private void CleanupChunk(Vector3Int position)
    {
        if (_chunkMeshes.TryGetValue(position, out Mesh mesh))
        {
            _meshPool.Release(mesh);
            _chunkMeshes.Remove(position);
        }
        if (_renderedChunks.TryGetValue(position, out var chunkGO))
        {
            Object.Destroy(chunkGO);
            _renderedChunks.Remove(position);
        }
        _propertyBlocks.Remove(position);
    }
}