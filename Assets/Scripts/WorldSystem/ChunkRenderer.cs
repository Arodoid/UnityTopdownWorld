using System.Collections.Generic;
using UnityEngine;

public class ChunkRenderer
{
    private readonly Dictionary<Vector3Int, GameObject> _renderedChunks = new Dictionary<Vector3Int, GameObject>();
    private Material terrainMaterial;

    public ChunkRenderer()
    {
        terrainMaterial = new Material(Shader.Find("Custom/TerrainShadowShader"));
    }

    public void RenderChunk(Chunk chunk, Mesh mesh)
    {
        Vector3Int position = chunk.Position;
        Vector3 worldPos = position * Chunk.ChunkSize;

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
            meshRenderer.material = terrainMaterial;

            _renderedChunks[position] = chunkGO;
        }
    }

    public void RemoveChunkRender(Vector3Int position)
    {
        if (_renderedChunks.TryGetValue(position, out var chunkGO))
        {
            Object.Destroy(chunkGO);
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
}