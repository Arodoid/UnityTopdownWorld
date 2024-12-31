using System.Collections.Generic;
using UnityEngine;

public class ChunkMeshGenerator
{
    private readonly int chunkSize = Chunk.ChunkSize;

    public Mesh GenerateMesh(Chunk chunk)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Color32> colors = new List<Color32>();
        List<Vector2> uvs = new List<Vector2>();

        for (int x = 0; x < chunkSize; x++)
        for (int y = 0; y < chunkSize; y++)
        for (int z = 0; z < chunkSize; z++)
        {
            Block block = chunk.GetBlock(x, y, z);
            if (block != null)
            {
                Vector3Int pos = new Vector3Int(x, y, z);
                
                switch (block.RenderType)
                {
                    case BlockRenderType.Cube:
                        AddCubeMesh(chunk, block, pos, vertices, triangles, colors, uvs);
                        break;
                    case BlockRenderType.Flat:
                        AddFlatMesh(block, pos, vertices, triangles, colors, uvs);
                        break;
                }
            }
        }

        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.colors32 = colors.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();
        return mesh;
    }

    private void AddCubeMesh(Chunk chunk, Block block, Vector3Int position,
                           List<Vector3> vertices, List<int> triangles, 
                           List<Color32> colors, List<Vector2> uvs)
    {
        int vertexIndex = vertices.Count;
        Vector2[] blockUVs = block.UVs;
        
        if (blockUVs == null || blockUVs.Length < 4)
        {
            blockUVs = new Vector2[]
            {
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero
            };
            // Debug.LogWarning($"Missing UVs for block: {block.Name}, falling back to color only");
        }

        // Get world position for deterministic rotation
        Vector3Int worldPos = chunk.Position * Chunk.ChunkSize + position;

        for (int i = 0; i < 6; i++)
        {
            Vector3Int neighborPos = position + BlockFaceNormals[i];

            if (!IsBlockOpaque(chunk, neighborPos))
            {
                // Add vertices for this face
                for (int v = 0; v < 4; v++)
                {
                    vertices.Add(BlockFaceVertices[i][v] + position);
                    colors.Add(block.Color);
                }

                // Handle UV rotation
                Vector2[] faceUVs = blockUVs;
                if (block.UseRandomRotation)
                {
                    int rotations = GetDeterministicRotation(worldPos, i);
                    faceUVs = RotateUVs(blockUVs, rotations);
                }

                // Add rotated UVs
                for (int v = 0; v < 4; v++)
                {
                    uvs.Add(faceUVs[v]);
                }

                // Add triangles
                triangles.AddRange(new int[]
                {
                    vertexIndex, vertexIndex + 1, vertexIndex + 2,
                    vertexIndex + 2, vertexIndex + 3, vertexIndex
                });

                vertexIndex += 4;
            }
        }
    }

    private void AddFlatMesh(Block block, Vector3Int position,
                           List<Vector3> vertices, List<int> triangles, 
                           List<Color32> colors, List<Vector2> uvs)
    {
        int vertexIndex = vertices.Count;
        float height = 1f;

        // Add vertices
        vertices.Add(new Vector3(position.x, position.y, position.z));
        vertices.Add(new Vector3(position.x + 1, position.y, position.z));
        vertices.Add(new Vector3(position.x + 1, position.y + height, position.z));
        vertices.Add(new Vector3(position.x, position.y + height, position.z));

        // Add UVs
        if (block.UVs != null && block.UVs.Length >= 4)
        {
            for (int i = 0; i < 4; i++)
            {
                uvs.Add(block.UVs[i]);
            }
        }
        else
        {
            // Fallback UVs
            uvs.Add(new Vector2(0, 0));
            uvs.Add(new Vector2(1, 0));
            uvs.Add(new Vector2(1, 1));
            uvs.Add(new Vector2(0, 1));
        }

        // Add colors
        for (int i = 0; i < 4; i++)
            colors.Add(block.Color);

        // Add triangles
        triangles.AddRange(new int[]
        {
            vertexIndex, vertexIndex + 1, vertexIndex + 2,
            vertexIndex + 2, vertexIndex + 3, vertexIndex
        });
    }

    private bool IsBlockOpaque(Chunk chunk, Vector3Int position)
    {
        if (position.x < 0 || position.x >= chunkSize ||
            position.y < 0 || position.y >= chunkSize ||
            position.z < 0 || position.z >= chunkSize)
        {
            return false;
        }

        Block block = chunk.GetBlock(position.x, position.y, position.z);
        return block != null && block.IsOpaque;
    }

    private static readonly Vector3Int[] BlockFaceNormals =
    {
        new Vector3Int(0, 0, 1),  // Front
        new Vector3Int(0, 0, -1), // Back
        new Vector3Int(0, 1, 0),  // Top
        new Vector3Int(0, -1, 0), // Bottom
        new Vector3Int(1, 0, 0),  // Right
        new Vector3Int(-1, 0, 0)  // Left
    };

    private static readonly Vector3[][] BlockFaceVertices =
    {
        // Front face (+z)
        new [] { new Vector3(0, 0, 1), new Vector3(1, 0, 1), new Vector3(1, 1, 1), new Vector3(0, 1, 1) },
        // Back face (-z)
        new [] { new Vector3(1, 0, 0), new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector3(1, 1, 0) },
        // Top face (+y)
        new [] { new Vector3(0, 1, 0), new Vector3(0, 1, 1), new Vector3(1, 1, 1), new Vector3(1, 1, 0) },
        // Bottom face (-y)
        new [] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 0, 1), new Vector3(0, 0, 1) },
        // Right face (+x)
        new [] { new Vector3(1, 0, 1), new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(1, 1, 1) },
        // Left face (-x)
        new [] { new Vector3(0, 0, 0), new Vector3(0, 0, 1), new Vector3(0, 1, 1), new Vector3(0, 1, 0) }
    };

    private Vector2[] RotateUVs(Vector2[] originalUVs, int rotations)
    {
        Vector2[] rotatedUVs = new Vector2[4];
        for (int i = 0; i < 4; i++)
        {
            rotatedUVs[i] = originalUVs[(i + rotations) % 4];
        }
        return rotatedUVs;
    }

    private int GetDeterministicRotation(Vector3Int worldPos, int face)
    {
        // Create a deterministic but seemingly random rotation based on position
        int seed = worldPos.x * 73856093 ^ worldPos.y * 19349663 ^ worldPos.z * 83492791 ^ face;
        System.Random random = new System.Random(seed);
        return random.Next(0, 4); // 0, 1, 2, or 3 rotations (90° each)
    }
}