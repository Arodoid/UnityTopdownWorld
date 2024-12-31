using System.Collections.Generic;
using UnityEngine;

public class ChunkMeshGenerator
{
    private readonly int chunkSize = Chunk.ChunkSize;

    public Mesh GenerateMesh(Chunk chunk, int yLimit)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Color32> colors = new List<Color32>();
        List<Vector2> uvs = new List<Vector2>();

        // Calculate effective Y limit within this chunk's space
        int chunkYOffset = chunk.Position.y * chunkSize;
        int effectiveYLimit = Mathf.Min(chunkSize, 
            Mathf.Max(0, yLimit - chunkYOffset));

        for (int x = 0; x < chunkSize; x++)
        for (int y = 0; y < effectiveYLimit; y++) // Only iterate to Y limit
        for (int z = 0; z < chunkSize; z++)
        {
            Block block = chunk.GetBlock(x, y, z);
            if (block != null)
            {
                Vector3Int pos = new Vector3Int(x, y, z);
                
                switch (block.RenderType)
                {
                    case BlockRenderType.Cube:
                        AddCubeMesh(chunk, block, pos, vertices, triangles, 
                                  colors, uvs, y == effectiveYLimit - 1);
                        break;
                    case BlockRenderType.Flat:
                        if (y < effectiveYLimit - 1) // Don't add flat meshes at the cut line
                        {
                            AddFlatMesh(block, pos, vertices, triangles, colors, uvs);
                        }
                        break;
                }
            }
        }

        // Add cap faces at Y-limit if needed
        if (effectiveYLimit < chunkSize)
        {
            AddYLevelCap(chunk, effectiveYLimit, vertices, triangles, colors, uvs);
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
                           List<Color32> colors, List<Vector2> uvs,
                           bool isAtYLimit)
    {
        int vertexIndex = vertices.Count;
        Vector2[] blockUVs = block.UVs ?? GetDefaultUVs();

        // Get world position for deterministic rotation
        Vector3Int worldPos = chunk.Position * Chunk.ChunkSize + position;

        // Iterate through all faces except bottom
        for (int i = 0; i < BlockFaceNormals.Length; i++)
        {
            Vector3Int neighborPos = position + BlockFaceNormals[i];
            bool isTopFace = i == 2; // Top face index

            // Special handling for faces at Y-limit
            if (isAtYLimit && isTopFace)
            {
                // Always show top face at Y-limit
                AddFace(i, position, block, blockUVs, worldPos,
                       vertices, triangles, colors, uvs);
            }
            else if (!IsBlockOpaque(chunk, neighborPos))
            {
                AddFace(i, position, block, blockUVs, worldPos,
                       vertices, triangles, colors, uvs);
            }
        }
    }

    private void AddYLevelCap(Chunk chunk, int yLevel,
                            List<Vector3> vertices, List<int> triangles,
                            List<Color32> colors, List<Vector2> uvs)
    {
        // Add a semi-transparent cap at the Y-level cut
        Color32 capColor = new Color32(128, 128, 128, 128); // Semi-transparent grey

        for (int x = 0; x < chunkSize; x++)
        for (int z = 0; z < chunkSize; z++)
        {
            // Skip if we're at the bottom of the chunk
            if (yLevel <= 0) continue;

            // Only add cap face if there's a block below
            Block blockBelow = null;
            if (yLevel > 0) // Check if we can safely look below
            {
                blockBelow = chunk.GetBlock(x, yLevel - 1, z);
            }

            if (blockBelow != null)
            {
                int vertexIndex = vertices.Count;
                Vector3 pos = new Vector3(x, yLevel, z);

                // Add vertices for cap face
                vertices.AddRange(new[]
                {
                    pos,
                    pos + Vector3.right,
                    pos + Vector3.right + Vector3.forward,
                    pos + Vector3.forward
                });

                // Add cap face triangles
                triangles.AddRange(new[]
                {
                    vertexIndex, vertexIndex + 1, vertexIndex + 2,
                    vertexIndex + 2, vertexIndex + 3, vertexIndex
                });

                // Add colors and UVs for cap face
                for (int i = 0; i < 4; i++)
                {
                    colors.Add(capColor);
                    uvs.Add(Vector2.zero); // Use a specific texture for the cap if desired
                }
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

    private Vector2[] GetDefaultUVs()
    {
        return new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(1, 1),
            new Vector2(0, 1)
        };
    }

    private void AddFace(int faceIndex, Vector3Int position, Block block, 
                        Vector2[] blockUVs, Vector3Int worldPos,
                        List<Vector3> vertices, List<int> triangles, 
                        List<Color32> colors, List<Vector2> uvs)
    {
        int vertexIndex = vertices.Count;

        // Add the four vertices for this face
        for (int i = 0; i < 4; i++)
        {
            vertices.Add(position + BlockFaceVertices[faceIndex][i]);
        }

        // Add triangles
        triangles.AddRange(new int[]
        {
            vertexIndex, vertexIndex + 1, vertexIndex + 2,
            vertexIndex + 2, vertexIndex + 3, vertexIndex
        });

        // Add colors
        for (int i = 0; i < 4; i++)
        {
            colors.Add(block.Color);
        }

        // Get rotated UVs based on world position
        int rotation = GetDeterministicRotation(worldPos, faceIndex);
        Vector2[] rotatedUVs = RotateUVs(blockUVs, rotation);
        uvs.AddRange(rotatedUVs);
    }

    public void GenerateMeshPooled(Chunk chunk, Mesh mesh, int yLimit)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Color32> colors = new List<Color32>();
        List<Vector2> uvs = new List<Vector2>();

        // Calculate effective Y limit within this chunk's space
        int chunkYOffset = chunk.Position.y * chunkSize;
        int effectiveYLimit = Mathf.Min(chunkSize, 
            Mathf.Max(0, yLimit - chunkYOffset));

        for (int x = 0; x < chunkSize; x++)
        for (int y = 0; y < effectiveYLimit; y++)
        for (int z = 0; z < chunkSize; z++)
        {
            Block block = chunk.GetBlock(x, y, z);
            if (block != null)
            {
                Vector3Int pos = new Vector3Int(x, y, z);
                
                switch (block.RenderType)
                {
                    case BlockRenderType.Cube:
                        AddCubeMesh(chunk, block, pos, vertices, triangles, 
                                  colors, uvs, y == effectiveYLimit - 1);
                        break;
                    case BlockRenderType.Flat:
                        if (y < effectiveYLimit - 1) // Don't add flat meshes at the cut line
                        {
                            AddFlatMesh(block, pos, vertices, triangles, colors, uvs);
                        }
                        break;
                }
            }
        }

        // Add cap faces at Y-limit if needed
        if (effectiveYLimit < chunkSize)
        {
            AddYLevelCap(chunk, effectiveYLimit, vertices, triangles, colors, uvs);
        }

        // Update the pooled mesh instead of creating a new one
        mesh.Clear();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.colors32 = colors.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();
    }
}