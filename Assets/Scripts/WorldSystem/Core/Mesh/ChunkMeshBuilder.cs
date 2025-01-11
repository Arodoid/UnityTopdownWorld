using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Burst;
using UnityEngine;
using System.Collections.Generic;
using WorldSystem.Data;
using WorldSystem.Jobs;
using WorldSystem.Base;

namespace WorldSystem.Mesh
{
    public class ChunkMeshBuilder : IChunkMeshBuilder
    {
        private const int VERTS_PER_QUAD = 4;
        private const int TRIS_PER_QUAD = 6;
        private const int TOTAL_QUADS = ChunkData.SIZE * ChunkData.SIZE;
        
        private readonly HashSet<int2> _meshesBeingBuilt = new();
        private readonly List<PendingMesh> _pendingMeshes = new();
        private readonly NativeArray<BlockDefinition> _blockDefs;

        public ChunkMeshBuilder()
        {
            _blockDefs = new NativeArray<BlockDefinition>(BlockColors.Definitions, Allocator.Persistent);
        }

        public void QueueMeshBuild(int2 position, NativeArray<byte> blocks, MeshFilter meshFilter, 
            MeshFilter shadowMeshFilter, int maxYLevel)
        {
            if (_meshesBeingBuilt.Contains(position))
                return;

            _meshesBeingBuilt.Add(position);

            var heightMap = new NativeArray<HeightPoint>(ChunkData.SIZE * ChunkData.SIZE, Allocator.TempJob);

            // Allocate mesh data arrays
            var vertices = new NativeArray<float3>(TOTAL_QUADS * VERTS_PER_QUAD, Allocator.TempJob);
            var triangles = new NativeArray<int>(TOTAL_QUADS * TRIS_PER_QUAD, Allocator.TempJob);
            var uvs = new NativeArray<float2>(TOTAL_QUADS * VERTS_PER_QUAD, Allocator.TempJob);
            var colors = new NativeArray<float4>(TOTAL_QUADS * VERTS_PER_QUAD, Allocator.TempJob);
            var normals = new NativeArray<float3>(TOTAL_QUADS * VERTS_PER_QUAD, Allocator.TempJob);
            var meshCounts = new NativeArray<int>(ChunkData.SIZE * 4, Allocator.TempJob);
            var shadowVertices = new NativeArray<float3>(TOTAL_QUADS * VERTS_PER_QUAD * 4, Allocator.TempJob);
            var shadowTriangles = new NativeArray<int>(TOTAL_QUADS * TRIS_PER_QUAD * 4, Allocator.TempJob);
            var shadowNormals = new NativeArray<float3>(TOTAL_QUADS * VERTS_PER_QUAD * 4, Allocator.TempJob);

            // Create a copy of the blocks array that we control
            var blocksCopy = new NativeArray<byte>(blocks.Length, Allocator.TempJob);
            blocksCopy.CopyFrom(blocks);

            var heightMapJob = new HeightMapGenerationJob
            {
                blocks = blocksCopy,
                heightMap = heightMap,
                maxYLevel = maxYLevel
            };

            var heightMapHandle = heightMapJob.Schedule(ChunkData.SIZE * ChunkData.SIZE, 64);

            var meshJob = new ChunkMeshJob
            {
                heightMap = heightMap,
                chunkPosition = position,
                vertices = vertices,
                triangles = triangles,
                uvs = uvs,
                colors = colors,
                normals = normals,
                blockDefinitions = _blockDefs,
                meshCounts = meshCounts,
                shadowVertices = shadowVertices,
                shadowTriangles = shadowTriangles,
                shadowNormals = shadowNormals
            };

            var jobHandle = meshJob.Schedule(ChunkData.SIZE, 1, heightMapHandle);

            _pendingMeshes.Add(new PendingMesh
            {
                position = position,
                jobHandle = jobHandle,
                vertices = vertices,
                triangles = triangles,
                uvs = uvs,
                colors = colors,
                normals = normals,
                meshFilter = meshFilter,
                heightMap = heightMap,
                meshCounts = meshCounts,
                shadowVertices = shadowVertices,
                shadowTriangles = shadowTriangles,
                shadowMeshFilter = shadowMeshFilter,
                shadowNormals = shadowNormals,
                blocks = blocksCopy  // Store the copy in the pending mesh
            });
        }

        public void QueueMeshBuild(int2 position, NativeArray<byte> blocks, MeshFilter meshFilter, 
            MeshFilter shadowMeshFilter)
        {
            QueueMeshBuild(position, blocks, meshFilter, shadowMeshFilter, ChunkData.HEIGHT);
        }

        public bool IsBuildingMesh(int2 position) => _meshesBeingBuilt.Contains(position);

        public void Update()
        {
            for (int i = _pendingMeshes.Count - 1; i >= 0; i--)
            {
                var pendingMesh = _pendingMeshes[i];
                if (pendingMesh.jobHandle.IsCompleted)
                {
                    pendingMesh.jobHandle.Complete();

                    // Create mesh data arrays on the job thread
                    var meshDataJob = new MeshDataPreparationJob
                    {
                        vertices = pendingMesh.vertices,
                        triangles = pendingMesh.triangles,
                        uvs = pendingMesh.uvs,
                        colors = pendingMesh.colors,
                        normals = pendingMesh.normals,
                        shadowVertices = pendingMesh.shadowVertices,
                        shadowTriangles = pendingMesh.shadowTriangles,
                        shadowNormals = pendingMesh.shadowNormals,
                        
                        // Output arrays
                        preparedVertices = new NativeArray<Vector3>(pendingMesh.vertices.Length, Allocator.TempJob),
                        preparedTriangles = new NativeArray<int>(pendingMesh.triangles.Length, Allocator.TempJob),
                        preparedUVs = new NativeArray<Vector2>(pendingMesh.uvs.Length, Allocator.TempJob),
                        preparedColors = new NativeArray<Color>(pendingMesh.colors.Length, Allocator.TempJob),
                        preparedNormals = new NativeArray<Vector3>(pendingMesh.normals.Length, Allocator.TempJob),
                        preparedShadowVertices = new NativeArray<Vector3>(pendingMesh.shadowVertices.Length, Allocator.TempJob),
                        preparedShadowTriangles = new NativeArray<int>(pendingMesh.shadowTriangles.Length, Allocator.TempJob),
                        preparedShadowNormals = new NativeArray<Vector3>(pendingMesh.shadowNormals.Length, Allocator.TempJob)
                    };

                    var dataPreparationHandle = meshDataJob.Schedule();
                    dataPreparationHandle.Complete(); // We still need to complete this job since Unity's Mesh API is main-thread only

                    // Create meshes using the prepared data
                    var mesh = new UnityEngine.Mesh();
                    mesh.SetVertices(meshDataJob.preparedVertices);
                    mesh.SetTriangles(meshDataJob.preparedTriangles.ToArray(), 0);
                    mesh.SetUVs(0, meshDataJob.preparedUVs);
                    mesh.SetColors(meshDataJob.preparedColors);
                    mesh.SetNormals(meshDataJob.preparedNormals);
                    pendingMesh.meshFilter.mesh = mesh;

                    var shadowMesh = new UnityEngine.Mesh();
                    shadowMesh.SetVertices(meshDataJob.preparedShadowVertices);
                    shadowMesh.SetTriangles(meshDataJob.preparedShadowTriangles.ToArray(), 0);
                    shadowMesh.SetNormals(meshDataJob.preparedShadowNormals);
                    pendingMesh.shadowMeshFilter.mesh = shadowMesh;

                    // Cleanup prepared data
                    meshDataJob.preparedVertices.Dispose();
                    meshDataJob.preparedTriangles.Dispose();
                    meshDataJob.preparedUVs.Dispose();
                    meshDataJob.preparedColors.Dispose();
                    meshDataJob.preparedNormals.Dispose();
                    meshDataJob.preparedShadowVertices.Dispose();
                    meshDataJob.preparedShadowTriangles.Dispose();
                    meshDataJob.preparedShadowNormals.Dispose();

                    // Get the chunk's GameObject and handle activation
                    var chunkObject = pendingMesh.meshFilter.gameObject;
                    var chunkManager = chunkObject.transform.parent.GetComponent<ChunkManager>();
                    
                    if (chunkManager != null)
                    {
                        var oldYLevel = chunkManager.ViewMaxYLevel - 1;
                        var pos = new int2(
                            Mathf.RoundToInt(chunkObject.transform.position.x / ChunkData.SIZE),
                            Mathf.RoundToInt(chunkObject.transform.position.z / ChunkData.SIZE)
                        );
                        chunkManager.ChunkPool?.DeactivateChunk(pos, oldYLevel);
                    }

                    chunkObject.SetActive(true);

                    // Cleanup
                    CleanupPendingMesh(pendingMesh);
                    _pendingMeshes.RemoveAt(i);
                    _meshesBeingBuilt.Remove(pendingMesh.position);
                }
            }
        }

        private void CleanupPendingMesh(PendingMesh pendingMesh)
        {
            pendingMesh.vertices.Dispose();
            pendingMesh.triangles.Dispose();
            pendingMesh.uvs.Dispose();
            pendingMesh.colors.Dispose();
            pendingMesh.normals.Dispose();
            pendingMesh.meshCounts.Dispose();
            pendingMesh.heightMap.Dispose();
            pendingMesh.shadowVertices.Dispose();
            pendingMesh.shadowTriangles.Dispose();
            pendingMesh.shadowNormals.Dispose();
            pendingMesh.blocks.Dispose();  // Dispose of our copy
        }

        public void Dispose()
        {
            foreach (var pendingMesh in _pendingMeshes)
            {
                pendingMesh.jobHandle.Complete();
                CleanupPendingMesh(pendingMesh);
            }
            _pendingMeshes.Clear();
            _meshesBeingBuilt.Clear();
            
            if (_blockDefs.IsCreated)
                _blockDefs.Dispose();
        }

        private struct PendingMesh
        {
            public int2 position;
            public JobHandle jobHandle;
            public NativeArray<float3> vertices;
            public NativeArray<int> triangles;
            public NativeArray<float2> uvs;
            public NativeArray<float4> colors;
            public NativeArray<HeightPoint> heightMap;
            public MeshFilter meshFilter;
            public NativeArray<int> meshCounts;
            public NativeArray<float3> shadowVertices;
            public NativeArray<int> shadowTriangles;
            public MeshFilter shadowMeshFilter;
            public NativeArray<float3> shadowNormals;
            public NativeArray<byte> blocks;
            public NativeArray<float3> normals;
        }

        [BurstCompile]
        private struct MeshDataPreparationJob : IJob
        {
            // Input arrays
            [ReadOnly] public NativeArray<float3> vertices;
            [ReadOnly] public NativeArray<int> triangles;
            [ReadOnly] public NativeArray<float2> uvs;
            [ReadOnly] public NativeArray<float4> colors;
            [ReadOnly] public NativeArray<float3> normals;
            [ReadOnly] public NativeArray<float3> shadowVertices;
            [ReadOnly] public NativeArray<int> shadowTriangles;
            [ReadOnly] public NativeArray<float3> shadowNormals;

            // Output arrays
            public NativeArray<Vector3> preparedVertices;
            public NativeArray<int> preparedTriangles;
            public NativeArray<Vector2> preparedUVs;
            public NativeArray<Color> preparedColors;
            public NativeArray<Vector3> preparedNormals;
            public NativeArray<Vector3> preparedShadowVertices;
            public NativeArray<int> preparedShadowTriangles;
            public NativeArray<Vector3> preparedShadowNormals;

            public void Execute()
            {
                // Convert and copy mesh data
                for (int i = 0; i < vertices.Length; i++)
                {
                    preparedVertices[i] = vertices[i];
                    preparedUVs[i] = uvs[i];
                    preparedColors[i] = new Color(colors[i].x, colors[i].y, colors[i].z, colors[i].w);
                    preparedNormals[i] = normals[i];
                }

                for (int i = 0; i < triangles.Length; i++)
                {
                    preparedTriangles[i] = triangles[i];
                }

                for (int i = 0; i < shadowVertices.Length; i++)
                {
                    preparedShadowVertices[i] = shadowVertices[i];
                    preparedShadowNormals[i] = shadowNormals[i];
                }

                for (int i = 0; i < shadowTriangles.Length; i++)
                {
                    preparedShadowTriangles[i] = shadowTriangles[i];
                }
            }
        }
    }
} 