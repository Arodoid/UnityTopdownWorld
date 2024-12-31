using UnityEngine;
using System.Collections.Generic;

public class VisualizationManager : MonoBehaviour
{
    [System.Serializable]
    public class VisualizerSettings
    {
        public bool enabled = true;
        public Color color = Color.white;
        public float opacity = 0.8f;
    }

    [Header("Visualization Settings")]
    [SerializeField] private bool enableVisualizations = true;
    [SerializeField] private VisualizerSettings chunkWireframes = new();
    [SerializeField] private VisualizerSettings columnOverlays = new();
    
    private ChunkQueueProcessor chunkProcessor;
    private Dictionary<Vector3Int, (Color color, string label)> chunkVisuals = new();
    private Dictionary<Vector2Int, (Color color, string label)> columnVisuals = new();

    private void Awake()
    {
        chunkProcessor = FindAnyObjectByType<ChunkQueueProcessor>();
    }

    public void ShowChunkWireframe(Vector3Int chunkPos, Color color, string label = "")
    {
        if (!enableVisualizations || !chunkWireframes.enabled) return;
        chunkVisuals[chunkPos] = (color, label);
    }

    public void ShowColumnOverlay(Vector2Int columnPos, Color color, string label = "")
    {
        if (!enableVisualizations || !columnOverlays.enabled) return;
        columnVisuals[columnPos] = (color, label);
    }

    public void ClearVisualizations()
    {
        chunkVisuals.Clear();
        columnVisuals.Clear();
    }

    private void OnDrawGizmos()
    {
        if (!enableVisualizations || chunkProcessor == null) return;

        // Only draw for currently visible chunks
        foreach (var chunkPos in chunkProcessor.CurrentlyVisibleChunks)
        {
            // Draw chunk wireframes
            if (chunkWireframes.enabled && chunkVisuals.TryGetValue(chunkPos, out var chunkData))
            {
                DrawChunkWireframe(chunkPos, chunkData.color, chunkData.label);
            }

            // Draw column overlays
            if (columnOverlays.enabled)
            {
                Vector2Int columnPos = new(chunkPos.x, chunkPos.z);
                if (columnVisuals.TryGetValue(columnPos, out var columnData))
                {
                    DrawColumnOverlay(columnPos, columnData.color, columnData.label);
                }
            }
        }
    }

    private void DrawChunkWireframe(Vector3Int chunkPos, Color color, string label)
    {
        Vector3 worldPos = new(
            chunkPos.x * Chunk.ChunkSize + Chunk.ChunkSize/2f,
            chunkPos.y * Chunk.ChunkSize + Chunk.ChunkSize/2f,
            chunkPos.z * Chunk.ChunkSize + Chunk.ChunkSize/2f
        );

        color.a = chunkWireframes.opacity;
        Gizmos.color = color;
        
        Vector3 size = Vector3.one * Chunk.ChunkSize;
        Gizmos.DrawWireCube(worldPos, size);

        if (!string.IsNullOrEmpty(label))
        {
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(worldPos, label);
            #endif
        }
    }

    private void DrawColumnOverlay(Vector2Int columnPos, Color color, string label)
    {
        Vector3 worldPos = new(
            columnPos.x * Chunk.ChunkSize + Chunk.ChunkSize/2f,
            100f, // Fixed height for column overlays
            columnPos.y * Chunk.ChunkSize + Chunk.ChunkSize/2f
        );

        color.a = columnOverlays.opacity;
        Gizmos.color = color;
        
        Vector3 size = new(Chunk.ChunkSize, 0.1f, Chunk.ChunkSize);
        Gizmos.DrawCube(worldPos, size);

        if (!string.IsNullOrEmpty(label))
        {
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(worldPos + Vector3.up, label);
            #endif
        }
    }
}