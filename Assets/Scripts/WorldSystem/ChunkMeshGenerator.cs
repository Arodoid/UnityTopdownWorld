using UnityEngine;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using VoxelGame.Interfaces;
using VoxelGame.Utilities;
using VoxelGame.WorldSystem.Jobs;

namespace VoxelGame.WorldSystem
{
    public class ChunkMeshGenerator : MonoBehaviour, IChunkMeshGenerator
    {
        [SerializeField] private IChunkRenderer chunkRenderer;
        [SerializeField] private IChunkManager chunkManager;
        
        private ObjectPool<Mesh> meshPool;
        private Dictionary<Vector3Int, JobHandle> pendingJobs = new Dictionary<Vector3Int, JobHandle>();

        private void Awake()
        {
            meshPool = new ObjectPool<Mesh>(
                () => new Mesh(),
                mesh => { if(mesh) mesh.Clear(); }
            );
            
            if (chunkRenderer == null) chunkRenderer = GetComponent<IChunkRenderer>();
            if (chunkManager == null) chunkManager = GetComponent<IChunkManager>();
        }

        public bool HasPendingJob(Vector3Int chunkPos) => pendingJobs.ContainsKey(chunkPos);

        public bool GenerateMeshData(Chunk chunk, Mesh mesh, int maxYLevel)
        {
            return GenerateMeshData(chunk, mesh, maxYLevel, MeshLODLevel.High);
        }

        private bool GenerateMeshData(Chunk chunk, Mesh mesh, int maxYLevel, MeshLODLevel lodLevel)
        {
            PerformanceMonitor.StartMeasurement("MeshGen.GenerateMeshData");
            if (chunk == null || chunk.IsFullyEmpty()) return true;

            // Convert chunk data to job-friendly format
            var jobData = new ChunkJobData
            {
                chunkPosition = new int3(chunk.Position.x, chunk.Position.y, chunk.Position.z),
                chunkSize = Chunk.ChunkSize,
                maxYLevel = Mathf.Min(Chunk.ChunkSize, maxYLevel - chunk.Position.y * Chunk.ChunkSize),
                blocks = new NativeArray<BlockData>(
                    Chunk.ChunkSize * Chunk.ChunkSize * Chunk.ChunkSize, 
                    Allocator.TempJob
                ),
                lodLevel = lodLevel
            };

            // Fill block data
            for (int x = 0; x < Chunk.ChunkSize; x++)
            for (int y = 0; y < Chunk.ChunkSize; y++)
            for (int z = 0; z < Chunk.ChunkSize; z++)
            {
                var block = chunk.GetBlock(x, y, z);
                int index = (y * Chunk.ChunkSize * Chunk.ChunkSize) + (x * Chunk.ChunkSize) + z;
                
                if (block != null)
                {
                    // Debug UV coordinates                    
                    jobData.blocks[index] = new BlockData
                    {
                        blockType = (byte)block.BlockType,
                        color = new float4(
                            block.Color.r / 255f, 
                            block.Color.g / 255f, 
                            block.Color.b / 255f, 
                            block.Color.a / 255f
                        ),
                        uvStart = float2.zero, // Not used anymore
                        isOpaque = block.IsOpaque
                    };
                }
            }

            // Create output containers
            var vertices = new NativeList<float3>(Allocator.TempJob);
            var triangles = new NativeList<int>(Allocator.TempJob);
            var uvs = new NativeList<float2>(Allocator.TempJob);
            var colors = new NativeList<float4>(Allocator.TempJob);
            
            // Allocate merged array
            var merged = new NativeArray<bool>(
                Chunk.ChunkSize * Chunk.ChunkSize, 
                Allocator.TempJob
            );

            // Create and schedule the job
            var job = new MeshGenerationJob
            {
                chunkData = jobData,
                vertices = vertices,
                triangles = triangles,
                uvs = uvs,
                colors = colors,
                merged = merged // Assign the merged array
            };

            var handle = job.Schedule();
            handle.Complete();

            // Apply results to mesh
            if (mesh != null)
            {
                ApplyJobResults(mesh, vertices, triangles, uvs, colors);
            }

            // Cleanup
            jobData.blocks.Dispose();
            vertices.Dispose();
            triangles.Dispose();
            uvs.Dispose();
            colors.Dispose();
            merged.Dispose(); // Don't forget to dispose the merged array

            PerformanceMonitor.EndMeasurement("MeshGen.GenerateMeshData");
            return true;
        }

        private void ApplyJobResults(
            Mesh mesh, 
            NativeList<float3> vertices, 
            NativeList<int> triangles,
            NativeList<float2> uvs,
            NativeList<float4> colors)
        {
            // Convert NativeArray data to Unity types
            var unityVertices = new Vector3[vertices.Length];
            var unityUVs = new Vector2[uvs.Length];
            var unityColors = new Color32[colors.Length];
            var unityTriangles = new int[triangles.Length];

            for (int i = 0; i < vertices.Length; i++)
            {
                unityVertices[i] = new Vector3(vertices[i].x, vertices[i].y, vertices[i].z);
            }

            for (int i = 0; i < uvs.Length; i++)
            {
                unityUVs[i] = new Vector2(uvs[i].x, uvs[i].y);
            }

            for (int i = 0; i < colors.Length; i++)
            {
                unityColors[i] = new Color32(
                    (byte)(colors[i].x * 255),
                    (byte)(colors[i].y * 255),
                    (byte)(colors[i].z * 255),
                    (byte)(colors[i].w * 255)
                );
            }

            // Copy triangles to regular array
            for (int i = 0; i < triangles.Length; i++)
            {
                unityTriangles[i] = triangles[i];
            }

            mesh.Clear();
            mesh.SetVertices(unityVertices);
            mesh.SetTriangles(unityTriangles, 0); // Now using the regular array
            mesh.SetUVs(0, unityUVs);
            mesh.SetColors(unityColors);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        public void CancelJob(Vector3Int chunkPos)
        {
            if (pendingJobs.TryGetValue(chunkPos, out JobHandle handle))
            {
                handle.Complete();
                pendingJobs.Remove(chunkPos);
            }
        }

        private void OnDestroy()
        {
            foreach (var handle in pendingJobs.Values)
            {
                handle.Complete();
            }
            pendingJobs.Clear();
        }
    }
}