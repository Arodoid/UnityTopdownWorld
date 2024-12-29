using UnityEngine;

public static class CoordinateUtility
{
    /// <summary>
    /// Converts a world position to chunk coordinates.
    /// Adjusts the world position to match the chunk grid.
    /// </summary>
    /// <param name="worldPosition">The position in world space.</param>
    /// <returns>The corresponding chunk position as a Vector3Int.</returns>
    public static Vector3Int WorldToChunkPosition(Vector3 worldPosition)
    {
        int chunkSize = Chunk.ChunkSize;

        int chunkX = Mathf.FloorToInt(worldPosition.x / chunkSize);
        int chunkY = Mathf.FloorToInt(worldPosition.y / chunkSize);
        int chunkZ = Mathf.FloorToInt(worldPosition.z / chunkSize);

        return new Vector3Int(chunkX, chunkY, chunkZ);
    }

    /// <summary>
    /// Converts chunk coordinates back into the starting world position of that chunk.
    /// </summary>
    /// <param name="chunkPosition">The chunk's position in chunk coordinates.</param>
    /// <returns>The world position for the origin of this chunk.</returns>
    public static Vector3Int ChunkToWorldPosition(Vector3Int chunkPosition)
    {
        int chunkSize = Chunk.ChunkSize;

        int worldX = chunkPosition.x * chunkSize;
        int worldY = chunkPosition.y * chunkSize;
        int worldZ = chunkPosition.z * chunkSize;

        return new Vector3Int(worldX, worldY, worldZ);
    }
}