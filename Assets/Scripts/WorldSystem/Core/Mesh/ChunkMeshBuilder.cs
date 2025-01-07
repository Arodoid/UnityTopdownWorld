using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
using System.Collections.Generic;
using WorldSystem.Data;
using WorldSystem.Jobs;

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

        public void QueueMeshBuild(int2 position, NativeArray<HeightPoint> heightMap, MeshFilter meshFilter, MeshFilter shadowMeshFilter)
        {
            if (_meshesBeingBuilt.Contains(position))
                return;

            _meshesBeingBuilt.Add(position);

            // Allocate mesh data arrays
            var vertices = new NativeArray<float3>(TOTAL_QUADS * VERTS_PER_QUAD, Allocator.Persistent);
            var triangles = new NativeArray<int>(TOTAL_QUADS * TRIS_PER_QUAD, Allocator.Persistent);
            var uvs = new NativeArray<float2>(TOTAL_QUADS * VERTS_PER_QUAD, Allocator.Persistent);
            var colors = new NativeArray<float4>(TOTAL_QUADS * VERTS_PER_QUAD, Allocator.Persistent);
            var meshCounts = new NativeArray<int>(ChunkData.SIZE * 4, Allocator.Persistent);
            var shadowVertices = new NativeArray<float3>(TOTAL_QUADS * VERTS_PER_QUAD * 4, Allocator.Persistent);
            var shadowTriangles = new NativeArray<int>(TOTAL_QUADS * TRIS_PER_QUAD * 4, Allocator.Persistent);

            var meshJob = new ChunkMeshJob
            {
                heightMap = heightMap,
                chunkPosition = position,
                vertices = vertices,
                triangles = triangles,
                uvs = uvs,
                colors = colors,
                blockDefinitions = _blockDefs,
                meshCounts = meshCounts,
                shadowVertices = shadowVertices,
                shadowTriangles = shadowTriangles
            };

            var jobHandle = meshJob.Schedule(ChunkData.SIZE, 1);

            _pendingMeshes.Add(new PendingMesh
            {
                position = position,
                jobHandle = jobHandle,
                vertices = vertices,
                triangles = triangles,
                uvs = uvs,
                colors = colors,
                meshFilter = meshFilter,
                heightMap = heightMap,
                meshCounts = meshCounts,
                shadowVertices = shadowVertices,
                shadowTriangles = shadowTriangles,
                shadowMeshFilter = shadowMeshFilter
            });
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

                    // Create render mesh
                    var mesh = new UnityEngine.Mesh();
                    mesh.SetVertices(pendingMesh.vertices.Reinterpret<Vector3>());
                    mesh.SetTriangles(pendingMesh.triangles.ToArray(), 0);
                    mesh.SetUVs(0, pendingMesh.uvs.Reinterpret<Vector2>());
                    mesh.SetColors(pendingMesh.colors.Reinterpret<Color>());
                    mesh.RecalculateNormals();
                    pendingMesh.meshFilter.mesh = mesh;

                    // Create shadow mesh
                    var shadowMesh = new UnityEngine.Mesh();
                    shadowMesh.SetVertices(pendingMesh.shadowVertices.Reinterpret<Vector3>());
                    shadowMesh.SetTriangles(pendingMesh.shadowTriangles.ToArray(), 0);
                    shadowMesh.RecalculateNormals();
                    pendingMesh.shadowMeshFilter.mesh = shadowMesh;

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
            pendingMesh.meshCounts.Dispose();
            pendingMesh.heightMap.Dispose();
            pendingMesh.shadowVertices.Dispose();
            pendingMesh.shadowTriangles.Dispose();
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
        }
    }
} 