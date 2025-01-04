using UnityEngine;
using System.Collections.Generic;
using VoxelGame.Interfaces;
using VoxelGame.Utilities;

namespace VoxelGame.WorldSystem
{
    public class ChunkMeshGenerator : MonoBehaviour, IChunkMeshGenerator
    {
        [SerializeField] private IChunkRenderer chunkRenderer;
        [SerializeField] private IChunkManager chunkManager;
        
        private ObjectPool<Mesh> meshPool;
        private Dictionary<Vector3Int, float> chunkUpdateTimes = new Dictionary<Vector3Int, float>();

        private void Awake()
        {
            meshPool = new ObjectPool<Mesh>(
                () => new Mesh(),
                mesh => { if(mesh) mesh.Clear(); }
            );
            
            if (chunkRenderer == null) chunkRenderer = GetComponent<IChunkRenderer>();
            if (chunkManager == null) chunkManager = GetComponent<IChunkManager>();
        }

        public bool HasPendingJob(Vector3Int chunkPos) => false;

        public bool GenerateMeshData(Chunk chunk, Mesh mesh, int maxYLevel)
        {
            if (chunk == null || chunk.IsFullyEmpty()) return true;

            Vector3Int chunkPos = chunk.Position;
            chunkUpdateTimes[chunkPos] = Time.time;

            var meshData = GenerateMeshDataImmediate(chunk, maxYLevel);
            if (mesh != null)
            {
                ApplyMeshData(mesh, meshData);
            }
            
            return true;
        }

        private (Vector3[] vertices, int[] triangles, Vector2[] uvs, Color32[] colors) GenerateMeshDataImmediate(Chunk chunk, int maxYLevel)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var uvs = new List<Vector2>();
            var colors = new List<Color32>();

            int effectiveYLimit = Mathf.Min(Chunk.ChunkSize, Mathf.Max(0, maxYLevel - chunk.Position.y * Chunk.ChunkSize));

            // Process blocks in natural order
            for (int x = 0; x < Chunk.ChunkSize; x++)
            for (int y = 0; y < effectiveYLimit; y++)
            for (int z = 0; z < Chunk.ChunkSize; z++)
            {
                ProcessBlock(chunk, x, y, z, vertices, triangles, uvs, colors);
            }

            return (vertices.ToArray(), triangles.ToArray(), uvs.ToArray(), colors.ToArray());
        }

        private void ProcessBlock(Chunk chunk, int x, int y, int z, 
            List<Vector3> vertices, List<int> triangles, List<Vector2> uvs, List<Color32> colors)
        {
            var block = chunk.GetBlock(x, y, z);
            if (block == null) return;

            // Check each face
            for (int face = 0; face < 6; face++)
            {
                if (ShouldRenderFace(chunk, x, y, z, face))
                {
                    AddFace(chunk, x, y, z, face, vertices, triangles, uvs, colors);
                }
            }
        }

        private bool ShouldRenderFace(Chunk chunk, int x, int y, int z, int face)
        {
            var normal = GetFaceNormal(face);
            int nx = x + normal.x;
            int ny = y + normal.y;
            int nz = z + normal.z;

            if (IsInChunkBounds(nx, ny, nz))
            {
                return chunk.GetBlock(nx, ny, nz) == null;
            }
            
            // Check neighbor chunks
            var neighborChunk = GetNeighborChunk(chunk, nx, ny, nz);
            if (neighborChunk != null)
            {
                var localPos = WrapCoordinates(nx, ny, nz);
                return neighborChunk.GetBlock(localPos.x, localPos.y, localPos.z) == null;
            }
            
            return true;
        }

        private void AddFace(Chunk chunk, int x, int y, int z, int face, 
            List<Vector3> vertices, List<int> triangles, List<Vector2> uvs, List<Color32> colors)
        {
            int vertexStart = vertices.Count;
            Vector3 pos = new Vector3(x, y, z);

            // Add vertices based on face
            switch (face)
            {
                case 0: // Right (positive X) - Fixing winding order
                    vertices.AddRange(new[] {
                        pos + new Vector3(1, 0, 1),  // Changed order
                        pos + new Vector3(1, 0, 0),
                        pos + new Vector3(1, 1, 0),
                        pos + new Vector3(1, 1, 1)
                    });
                    break;
                case 1: // Left (negative X) - Fixing winding order
                    vertices.AddRange(new[] {
                        pos + new Vector3(0, 0, 0),  // Changed order
                        pos + new Vector3(0, 0, 1),
                        pos + new Vector3(0, 1, 1),
                        pos + new Vector3(0, 1, 0)
                    });
                    break;
                case 2: // Top
                    vertices.AddRange(new[] {
                        pos + new Vector3(0, 1, 1),
                        pos + new Vector3(1, 1, 1),
                        pos + new Vector3(1, 1, 0),
                        pos + new Vector3(0, 1, 0)
                    });
                    break;
                case 3: // Front
                    vertices.AddRange(new[] {
                        pos + new Vector3(0, 0, 1),
                        pos + new Vector3(1, 0, 1),
                        pos + new Vector3(1, 1, 1),
                        pos + new Vector3(0, 1, 1)
                    });
                    break;
                case 4: // Back
                    vertices.AddRange(new[] {
                        pos + new Vector3(1, 0, 0),
                        pos + new Vector3(0, 0, 0),
                        pos + new Vector3(0, 1, 0),
                        pos + new Vector3(1, 1, 0)
                    });
                    break;
            }

            // Get the block at this position
            var block = chunk.GetBlock(x, y, z);
            
            // Add UVs from the block's atlas coordinates
            uvs.AddRange(block.UVs);

            // Use the block's color instead of face color
            var blockColor = block.Color;
            for (int i = 0; i < 4; i++)
            {
                colors.Add(blockColor);
            }

            // Add triangles
            triangles.AddRange(new[] {
                vertexStart, vertexStart + 1, vertexStart + 2,
                vertexStart, vertexStart + 2, vertexStart + 3
            });
        }

        private Vector3Int GetFaceNormal(int face)
        {
            switch (face)
            {
                case 0: return new Vector3Int(1, 0, 0);   // Right
                case 1: return new Vector3Int(-1, 0, 0);  // Left
                case 2: return new Vector3Int(0, 1, 0);   // Top
                case 3: return new Vector3Int(0, 0, 1);   // Front
                case 4: return new Vector3Int(0, 0, -1);  // Back
                default: return new Vector3Int(0, 0, 0);
            }
        }

        private bool IsInChunkBounds(int x, int y, int z)
        {
            return x >= 0 && x < Chunk.ChunkSize && 
                   y >= 0 && y < Chunk.ChunkSize && 
                   z >= 0 && z < Chunk.ChunkSize;
        }

        private Vector3Int WrapCoordinates(int x, int y, int z)
        {
            return new Vector3Int(
                (x + Chunk.ChunkSize) % Chunk.ChunkSize,
                (y + Chunk.ChunkSize) % Chunk.ChunkSize,
                (z + Chunk.ChunkSize) % Chunk.ChunkSize
            );
        }

        private Chunk GetNeighborChunk(Chunk chunk, int x, int y, int z)
        {
            var chunkPos = chunk.Position;
            if (x < 0) return chunkManager.GetChunk(new Vector3Int(chunkPos.x - 1, chunkPos.y, chunkPos.z));
            if (x >= Chunk.ChunkSize) return chunkManager.GetChunk(new Vector3Int(chunkPos.x + 1, chunkPos.y, chunkPos.z));
            if (y < 0) return chunkManager.GetChunk(new Vector3Int(chunkPos.x, chunkPos.y - 1, chunkPos.z));
            if (y >= Chunk.ChunkSize) return chunkManager.GetChunk(new Vector3Int(chunkPos.x, chunkPos.y + 1, chunkPos.z));
            if (z < 0) return chunkManager.GetChunk(new Vector3Int(chunkPos.x, chunkPos.y, chunkPos.z - 1));
            if (z >= Chunk.ChunkSize) return chunkManager.GetChunk(new Vector3Int(chunkPos.x, chunkPos.y, chunkPos.z + 1));
            return null;
        }

        private void ApplyMeshData(Mesh mesh, (Vector3[] vertices, int[] triangles, Vector2[] uvs, Color32[] colors) data)
        {
            mesh.SetVertices(data.vertices);
            mesh.SetTriangles(data.triangles, 0);
            mesh.SetUVs(0, data.uvs);
            mesh.SetColors(data.colors);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        public void CancelJob(Vector3Int chunkPos)
        {
            // No jobs to cancel in this simplified version
            chunkUpdateTimes.Remove(chunkPos);
        }
    }
}