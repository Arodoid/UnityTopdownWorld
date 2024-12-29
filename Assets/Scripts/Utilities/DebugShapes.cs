using UnityEngine;

public static class DebugShapes
{
    public static void DrawOrthographicFrustum(Camera camera, Color color, float yOffset = 0f)
    {
        float width = camera.orthographicSize * 2 * camera.aspect;
        float height = camera.orthographicSize * 2;
        Vector3 center = camera.transform.position;
        center.y = yOffset;  // Allow drawing at different Y levels

        Vector3 topLeft = center + new Vector3(-width/2, 0, height/2);
        Vector3 topRight = center + new Vector3(width/2, 0, height/2);
        Vector3 bottomLeft = center + new Vector3(-width/2, 0, -height/2);
        Vector3 bottomRight = center + new Vector3(width/2, 0, -height/2);
        
        Debug.DrawLine(topLeft, topRight, color);
        Debug.DrawLine(topRight, bottomRight, color);
        Debug.DrawLine(bottomRight, bottomLeft, color);
        Debug.DrawLine(bottomLeft, topLeft, color);
    }

    public static void DrawCircle(Vector3 center, float radius, Color color, int segments = 32)
    {
        float angleStep = 360f / segments;
        
        for (int i = 0; i < segments; i++)
        {
            float angle1 = i * angleStep * Mathf.Deg2Rad;
            float angle2 = (i + 1) * angleStep * Mathf.Deg2Rad;
            
            Vector3 point1 = center + new Vector3(Mathf.Cos(angle1) * radius, 0f, Mathf.Sin(angle1) * radius);
            Vector3 point2 = center + new Vector3(Mathf.Cos(angle2) * radius, 0f, Mathf.Sin(angle2) * radius);
            
            Debug.DrawLine(point1, point2, color);
        }
    }

    public static void DrawChunkBounds(Vector3Int chunkPosition, float chunkSize, Color color)
    {
        Vector3 worldPos = new Vector3(
            chunkPosition.x * chunkSize,
            0,
            chunkPosition.z * chunkSize
        );

        Vector3 size = new Vector3(chunkSize, 0, chunkSize);
        DrawSquare(worldPos, size, color);
    }

    public static void DrawSquare(Vector3 center, Vector3 size, Color color)
    {
        Vector3 halfSize = size * 0.5f;
        
        Vector3 topLeft = center + new Vector3(-halfSize.x, 0, halfSize.z);
        Vector3 topRight = center + new Vector3(halfSize.x, 0, halfSize.z);
        Vector3 bottomLeft = center + new Vector3(-halfSize.x, 0, -halfSize.z);
        Vector3 bottomRight = center + new Vector3(halfSize.x, 0, -halfSize.z);
        
        Debug.DrawLine(topLeft, topRight, color);
        Debug.DrawLine(topRight, bottomRight, color);
        Debug.DrawLine(bottomRight, bottomLeft, color);
        Debug.DrawLine(bottomLeft, topLeft, color);
    }

    public static void DrawViewDistance(Vector3 center, float viewDistance, Color color)
    {
        DrawCircle(center, viewDistance, color);
    }

    // For drawing vertical lines at chunk corners
    public static void DrawVerticalLine(Vector3 position, float height, Color color)
    {
        Vector3 bottom = position;
        Vector3 top = position + Vector3.up * height;
        Debug.DrawLine(bottom, top, color);
    }

    public static void DrawWorldBounds(Camera camera, float viewDistance, int worldHeight, Color color)
    {
        float width = camera.orthographicSize * 2 * camera.aspect;
        float height = camera.orthographicSize * 2;
        Vector3 center = camera.transform.position;

        // Draw at ground level (Y = 0)
        DrawGridPlane(center, width, height, viewDistance, color * new Color(1, 1, 1, 0.2f), 0);
        
        // Draw at max world height
        float maxY = worldHeight * Chunk.ChunkSize;
        DrawGridPlane(center, width, height, viewDistance, color * new Color(1, 1, 1, 0.2f), maxY);
    }

    private static void DrawGridPlane(Vector3 center, float width, float height, float viewDistance, Color color, float yLevel)
    {
        // Calculate grid size based on chunk size
        float gridSize = Chunk.ChunkSize;
        
        // Calculate bounds
        float maxDistance = Mathf.Max(width, height, viewDistance * 2) * 0.5f;
        int gridCount = Mathf.CeilToInt(maxDistance / gridSize);

        // Draw X lines
        for (int i = -gridCount; i <= gridCount; i++)
        {
            Vector3 start = new Vector3(i * gridSize, yLevel, -maxDistance) + new Vector3(center.x, 0, center.z);
            Vector3 end = new Vector3(i * gridSize, yLevel, maxDistance) + new Vector3(center.x, 0, center.z);
            Debug.DrawLine(start, end, color);
        }

        // Draw Z lines
        for (int i = -gridCount; i <= gridCount; i++)
        {
            Vector3 start = new Vector3(-maxDistance, yLevel, i * gridSize) + new Vector3(center.x, 0, center.z);
            Vector3 end = new Vector3(maxDistance, yLevel, i * gridSize) + new Vector3(center.x, 0, center.z);
            Debug.DrawLine(start, end, color);
        }
    }
} 