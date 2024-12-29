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

        for (int i = 0; i < 6; i++)
        {
            Vector3Int neighborPos = position + BlockFaceNormals[i];

            if (!IsBlockOpaque(chunk, neighborPos))
            {
                foreach (Vector3 vertex in BlockFaceVertices[i])
                {
                    vertices.Add(vertex + position);
                    colors.Add(block.Color);
                    uvs.Add(Vector2.zero); // Solid blocks don't use UVs
                }

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

        // Get UV coordinates from sprite if available
        Rect uvRect = block.Sprite != null ? block.Sprite.rect : new Rect(0, 0, 1, 1);
        Vector2 uvScale = block.Sprite != null ? 
            new Vector2(1f / block.Sprite.texture.width, 1f / block.Sprite.texture.height) : 
            Vector2.one;

        // Add vertices with UVs
        vertices.Add(new Vector3(position.x, position.y, position.z));
        uvs.Add(new Vector2(uvRect.x * uvScale.x, uvRect.y * uvScale.y));

        vertices.Add(new Vector3(position.x + 1, position.y, position.z));
        uvs.Add(new Vector2((uvRect.x + uvRect.width) * uvScale.x, uvRect.y * uvScale.y));

        vertices.Add(new Vector3(position.x + 1, position.y + height, position.z));
        uvs.Add(new Vector2((uvRect.x + uvRect.width) * uvScale.x, (uvRect.y + uvRect.height) * uvScale.y));

        vertices.Add(new Vector3(position.x, position.y + height, position.z));
        uvs.Add(new Vector2(uvRect.x * uvScale.x, (uvRect.y + uvRect.height) * uvScale.y));

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
}