using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using System.Collections.Generic;
using System.Linq;

namespace WorldSystem.Mesh
{
    public class ChunkMeshManager
    {
        private struct PendingMesh
        {
            public int2 position;
            public JobHandle jobHandle;
            public NativeList<float3> vertices;
            public NativeList<int> triangles;
            public NativeList<float2> uvs;
            public NativeList<float4> colors;
            public NativeList<float3> normals;
            public MeshFilter meshFilter;
            public MeshFilter shadowFilter;
        }

        private readonly HashSet<int2> _meshesBeingBuilt = new();
        private readonly List<PendingMesh> _pendingMeshes = new();
        private readonly NativeArray<float4> _blockColors;

        public ChunkMeshManager(Data.BlockDefinition[] blockDefs)
        {
            _blockColors = new NativeArray<float4>(blockDefs.Length, Allocator.Persistent);
            for (int i = 0; i < blockDefs.Length; i++)
            {
                _blockColors[i] = blockDefs[i].color;
            }
        }

        public bool IsBuildingMesh(int2 position) => _meshesBeingBuilt.Contains(position);

        public void QueueMeshBuild(int2 position, NativeArray<byte> blocks, MeshFilter meshFilter, MeshFilter shadowFilter, int maxYLevel)
        {
            if (_meshesBeingBuilt.Contains(position))
                return;

            _meshesBeingBuilt.Add(position);

            var jobHandle = ChunkMeshGenerator.GenerateMesh(
                blocks,
                _blockColors,
                maxYLevel,
                out var vertices,
                out var triangles,
                out var uvs,
                out var colors,
                out var normals
            );

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
                shadowFilter = shadowFilter
            });
        }

        public void Update()
        {
            for (int i = _pendingMeshes.Count - 1; i >= 0; i--)
            {
                var pending = _pendingMeshes[i];
                if (pending.jobHandle.IsCompleted)
                {
                    pending.jobHandle.Complete();

                    // Debug check for data
                    if (pending.vertices.Length == 0)
                    {
                        Debug.LogWarning($"No vertices generated for chunk at {pending.position}");
                        CleanupPendingMesh(pending);
                        _meshesBeingBuilt.Remove(pending.position);
                        _pendingMeshes.RemoveAt(i);
                        continue;
                    }

                    var mesh = new UnityEngine.Mesh();
                    mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // Support larger meshes

                    // Convert data directly without LINQ for better performance
                    var vertexCount = pending.vertices.Length;
                    var vertices = new Vector3[vertexCount];
                    var uvs = new Vector2[vertexCount];
                    var colors = new Color[vertexCount];
                    var normals = new Vector3[vertexCount];

                    // Convert vertices and associated data
                    for (int v = 0; v < vertexCount; v++)
                    {
                        var vertex = pending.vertices[v];
                        vertices[v] = new Vector3(vertex.x, vertex.y, vertex.z);
                        
                        var uv = pending.uvs[v];
                        uvs[v] = new Vector2(uv.x, uv.y);
                        
                        var color = pending.colors[v];
                        colors[v] = new Color(color.x, color.y, color.z, color.w);
                        
                        var normal = pending.normals[v];
                        normals[v] = new Vector3(normal.x, normal.y, normal.z);
                    }

                    // Convert triangles
                    var triangles = pending.triangles.ToArray(Allocator.Temp).ToArray();

                    // Set mesh data
                    mesh.vertices = vertices;
                    mesh.triangles = triangles;
                    mesh.uv = uvs;
                    mesh.colors = colors;
                    mesh.normals = normals;

                    // Optimize mesh
                    mesh.RecalculateBounds();
                    mesh.Optimize();

                    // Apply mesh
                    pending.meshFilter.mesh = mesh;
                    if (pending.shadowFilter != null)
                    {
                        pending.shadowFilter.mesh = mesh;
                    }

                    // Enable the GameObject if it was disabled
                    pending.meshFilter.gameObject.SetActive(true);

                    CleanupPendingMesh(pending);
                    _meshesBeingBuilt.Remove(pending.position);
                    _pendingMeshes.RemoveAt(i);
                }
            }
        }

        private void CleanupPendingMesh(PendingMesh pending)
        {
            if (pending.vertices.IsCreated) pending.vertices.Dispose();
            if (pending.triangles.IsCreated) pending.triangles.Dispose();
            if (pending.uvs.IsCreated) pending.uvs.Dispose();
            if (pending.colors.IsCreated) pending.colors.Dispose();
            if (pending.normals.IsCreated) pending.normals.Dispose();
        }

        public void Dispose()
        {
            foreach (var pending in _pendingMeshes)
            {
                pending.jobHandle.Complete();
                pending.vertices.Dispose();
                pending.triangles.Dispose();
                pending.uvs.Dispose();
                pending.colors.Dispose();
                pending.normals.Dispose();
            }
            _pendingMeshes.Clear();
            _meshesBeingBuilt.Clear();

            if (_blockColors.IsCreated)
                _blockColors.Dispose();
        }
    }
} 