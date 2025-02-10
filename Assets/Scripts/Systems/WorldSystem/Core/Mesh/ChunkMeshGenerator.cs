using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
using WorldSystem.Data;

namespace WorldSystem.Mesh
{
    public static class ChunkMeshGenerator
    {
        private struct FaceData
        {
            public int3 position;
            public int2 size; // width, height
            public int direction;
            public byte blockType;
        }

        private struct NativeArray3D<T> where T : struct
        {
            private NativeArray<T> array;
            private readonly int sizeX, sizeY, sizeZ;

            public NativeArray3D(int x, int y, int z, Allocator allocator)
            {
                array = new NativeArray<T>(x * y * z, allocator);
                sizeX = x;
                sizeY = y;
                sizeZ = z;
            }

            public T this[int x, int y, int z]
            {
                get => array[x + y * sizeX + z * sizeX * sizeY];
                set => array[x + y * sizeX + z * sizeX * sizeY] = value;
            }

            public void Dispose()
            {
                array.Dispose();
            }
        }

        [BurstCompile]
        private struct CollectFacesJob : IJob
        {
            [ReadOnly] public NativeArray<byte> blocks;
            public NativeArray<bool> processedFaces;
            public NativeList<FaceData> faces;
            public int maxYLevel;

            private int GetProcessedIndex(int x, int y, int z, int direction) =>
                (direction * ChunkData.SIZE * maxYLevel * ChunkData.SIZE) + 
                (y * ChunkData.SIZE * ChunkData.SIZE) + 
                (z * ChunkData.SIZE) + x;

            private bool IsBlockFaceVisible(int x, int y, int z, int dx, int dy, int dz)
            {
                int nx = x + dx;
                int ny = y + dy;
                int nz = z + dz;

                if (nx < 0 || nx >= ChunkData.SIZE || nz < 0 || nz >= ChunkData.SIZE)
                    return true;

                if (ny < 0 || ny >= ChunkData.HEIGHT)
                    return true;

                return blocks[(ny * ChunkData.SIZE * ChunkData.SIZE) + (nz * ChunkData.SIZE) + nx] == 0;
            }

            private bool CanMergeBlocks(int x1, int y1, int z1, int x2, int y2, int z2)
            {
                if (x1 < 0 || x1 >= ChunkData.SIZE || y1 < 0 || y1 >= ChunkData.HEIGHT || 
                    z1 < 0 || z1 >= ChunkData.SIZE || x2 < 0 || x2 >= ChunkData.SIZE || 
                    y2 < 0 || y2 >= ChunkData.HEIGHT || z2 < 0 || z2 >= ChunkData.SIZE)
                    return false;

                int idx1 = (y1 * ChunkData.SIZE * ChunkData.SIZE) + (z1 * ChunkData.SIZE) + x1;
                int idx2 = (y2 * ChunkData.SIZE * ChunkData.SIZE) + (z2 * ChunkData.SIZE) + x2;
                
                return blocks[idx1] == blocks[idx2] && blocks[idx1] != 0;
            }

            public void Execute()
            {
                for (int y = 0; y < maxYLevel; y++)
                for (int z = 0; z < ChunkData.SIZE; z++)
                for (int x = 0; x < ChunkData.SIZE; x++)
                {
                    int idx = (y * ChunkData.SIZE * ChunkData.SIZE) + (z * ChunkData.SIZE) + x;
                    byte block = blocks[idx];
                    if (block == 0) continue;

                    // Check each direction
                    int[] directions = { 0, 1, 2, 3, 4, 5 }; // +X, -X, +Y, -Y, +Z, -Z
                    int3[] dirVectors = {
                        new int3(1,0,0), new int3(-1,0,0),
                        new int3(0,1,0), new int3(0,-1,0),
                        new int3(0,0,1), new int3(0,0,-1)
                    };

                    for (int d = 0; d < 6; d++)
                    {
                        int processedIdx = GetProcessedIndex(x, y, z, d);
                        if (processedFaces[processedIdx]) continue;

                        var dir = dirVectors[d];
                        if (!IsBlockFaceVisible(x, y, z, dir.x, dir.y, dir.z)) continue;

                        // Find maximum face size
                        int width = 1;
                        int height = 1;

                        // Try to expand in width and height based on direction
                        if (dir.x != 0)
                        {
                            // Expand in Z (width) and Y (height)
                            while (z + width < ChunkData.SIZE && 
                                   !processedFaces[GetProcessedIndex(x, y, z + width, d)] &&
                                   CanMergeBlocks(x, y, z, x, y, z + width) &&
                                   IsBlockFaceVisible(x, y, z + width, dir.x, 0, 0))
                            {
                                width++;
                            }

                            bool canExpandHeight = true;
                            while (canExpandHeight && y + height < maxYLevel)
                            {
                                for (int dz = 0; dz < width; dz++)
                                {
                                    if (processedFaces[GetProcessedIndex(x, y + height, z + dz, d)] ||
                                        !CanMergeBlocks(x, y, z, x, y + height, z + dz) ||
                                        !IsBlockFaceVisible(x, y + height, z + dz, dir.x, 0, 0))
                                    {
                                        canExpandHeight = false;
                                        break;
                                    }
                                }
                                if (canExpandHeight) height++;
                            }
                        }
                        // Similar expansion logic for Y and Z directions...

                        // Mark processed faces
                        for (int dy = 0; dy < height; dy++)
                        for (int dw = 0; dw < width; dw++)
                        {
                            int px = x, py = y + dy, pz = z + dw;
                            if (dir.y != 0) { px = x + dw; pz = z; }
                            else if (dir.z != 0) { px = x + dw; py = y + dy; }
                            
                            processedFaces[GetProcessedIndex(px, py, pz, d)] = true;
                        }

                        // Add face data
                        faces.Add(new FaceData
                        {
                            position = new int3(x, y, z),
                            size = new int2(width, height),
                            direction = d,
                            blockType = block
                        });
                    }
                }
            }
        }

        [BurstCompile]
        private struct GenerateMeshFromFacesJob : IJob
        {
            [ReadOnly] public NativeList<FaceData> faces;
            [ReadOnly] public NativeArray<float4> blockColors;
            public NativeList<float3> vertices;
            public NativeList<int> triangles;
            public NativeList<float2> uvs;
            public NativeList<float4> colors;
            public NativeList<float3> normals;

            private void AddFaceToMesh(FaceData face)
            {
                int vertexStart = vertices.Length;
                float4 color = blockColors[face.blockType];

                // Calculate normal based on direction
                float3 normal = float3.zero;
                switch (face.direction)
                {
                    case 0: normal = new float3(-1, 0, 0); break;  // +X (flipped)
                    case 1: normal = new float3(1, 0, 0); break;   // -X (flipped)
                    case 2: normal = new float3(0, 1, 0); break;   // +Y
                    case 3: normal = new float3(0, -1, 0); break;  // -Y
                    case 4: normal = new float3(0, 0, 1); break;   // +Z
                    case 5: normal = new float3(0, 0, -1); break;  // -Z
                }

                // Add vertices for the face
                float3 pos = face.position;
                float width = face.size.x;
                float height = face.size.y;

                // Add vertices based on face direction
                if (math.abs(normal.x) > 0)
                {
                    float x = pos.x + math.max(0, normal.x);
                    vertices.Add(new float3(x, pos.y, pos.z));
                    vertices.Add(new float3(x, pos.y + height, pos.z));
                    vertices.Add(new float3(x, pos.y + height, pos.z + width));
                    vertices.Add(new float3(x, pos.y, pos.z + width));
                }
                else if (math.abs(normal.y) > 0)
                {
                    float y = pos.y + math.max(0, normal.y);
                    vertices.Add(new float3(pos.x, y, pos.z));
                    vertices.Add(new float3(pos.x + width, y, pos.z));
                    vertices.Add(new float3(pos.x + width, y, pos.z + height));
                    vertices.Add(new float3(pos.x, y, pos.z + height));
                }
                else // Z face
                {
                    float z = pos.z + math.max(0, normal.z);
                    vertices.Add(new float3(pos.x, pos.y, z));
                    vertices.Add(new float3(pos.x, pos.y + height, z));
                    vertices.Add(new float3(pos.x + width, pos.y + height, z));
                    vertices.Add(new float3(pos.x + width, pos.y, z));
                }

                // Add UVs
                uvs.Add(new float2(0, 0));
                uvs.Add(new float2(0, height));
                uvs.Add(new float2(width, height));
                uvs.Add(new float2(width, 0));

                // Add colors and normals
                for (int i = 0; i < 4; i++)
                {
                    colors.Add(color);
                    normals.Add(normal);
                }

                // Add triangles (ensure proper winding order based on normal)
                if (normal.x > 0 || normal.y < 0 || normal.z > 0)
                {
                    triangles.Add(vertexStart);
                    triangles.Add(vertexStart + 1);
                    triangles.Add(vertexStart + 2);
                    triangles.Add(vertexStart);
                    triangles.Add(vertexStart + 2);
                    triangles.Add(vertexStart + 3);
                }
                else
                {
                    triangles.Add(vertexStart);
                    triangles.Add(vertexStart + 2);
                    triangles.Add(vertexStart + 1);
                    triangles.Add(vertexStart);
                    triangles.Add(vertexStart + 3);
                    triangles.Add(vertexStart + 2);
                }
            }

            public void Execute()
            {
                // Clear all lists first
                vertices.Clear();
                triangles.Clear();
                uvs.Clear();
                colors.Clear();
                normals.Clear();

                // Generate mesh data from face data
                for (int i = 0; i < faces.Length; i++)
                {
                    AddFaceToMesh(faces[i]);
                }
            }
        }

        [BurstCompile]
        private struct GenerateMeshJob : IJob
        {
            [ReadOnly] public NativeArray<byte> blocks;
            [ReadOnly] public NativeArray<float4> blockColors;
            public int maxYLevel;
            public NativeList<float3> vertices;
            public NativeList<int> triangles;
            public NativeList<float2> uvs;
            public NativeList<float4> colors;
            public NativeList<float3> normals;

            // Add [DeallocateOnJobCompletion] to auto-dispose these arrays
            [DeallocateOnJobCompletion] public NativeArray3D<bool> processedXPos;
            [DeallocateOnJobCompletion] public NativeArray3D<bool> processedXNeg;
            [DeallocateOnJobCompletion] public NativeArray3D<bool> processedYPos;
            [DeallocateOnJobCompletion] public NativeArray3D<bool> processedYNeg;
            [DeallocateOnJobCompletion] public NativeArray3D<bool> processedZPos;
            [DeallocateOnJobCompletion] public NativeArray3D<bool> processedZNeg;

            private bool IsBlockFaceVisible(int x, int y, int z, int dx, int dy, int dz)
            {
                int nx = x + dx;
                int ny = y + dy;
                int nz = z + dz;

                if (nx < 0 || nx >= ChunkData.SIZE || nz < 0 || nz >= ChunkData.SIZE)
                    return true;

                if (ny < 0 || ny >= ChunkData.HEIGHT)
                    return true;

                return blocks[(ny * ChunkData.SIZE * ChunkData.SIZE) + (nz * ChunkData.SIZE) + nx] == 0;
            }

            private bool CanMergeBlocks(int x1, int y1, int z1, int x2, int y2, int z2)
            {
                if (x1 < 0 || x1 >= ChunkData.SIZE || y1 < 0 || y1 >= ChunkData.HEIGHT || z1 < 0 || z1 >= ChunkData.SIZE ||
                    x2 < 0 || x2 >= ChunkData.SIZE || y2 < 0 || y2 >= ChunkData.HEIGHT || z2 < 0 || z2 >= ChunkData.SIZE)
                    return false;

                int idx1 = (y1 * ChunkData.SIZE * ChunkData.SIZE) + (z1 * ChunkData.SIZE) + x1;
                int idx2 = (y2 * ChunkData.SIZE * ChunkData.SIZE) + (z2 * ChunkData.SIZE) + x2;
                
                return blocks[idx1] == blocks[idx2] && blocks[idx1] != 0;
            }

            private void AddGreedyFace(int startX, int startY, int startZ, int width, int height, float3 normal, byte blockType)
            {
                int vertexStart = vertices.Length;
                float4 color = blockColors[blockType];

                if (math.abs(normal.x) > 0)
                {
                    float x0 = startX + math.max(0, normal.x);
                    if (normal.x > 0)
                    {
                        vertices.Add(new float3(x0, startY, startZ));
                        vertices.Add(new float3(x0, startY + height, startZ));
                        vertices.Add(new float3(x0, startY + height, startZ + width));
                        vertices.Add(new float3(x0, startY, startZ + width));
                    }
                    else
                    {
                        vertices.Add(new float3(x0, startY, startZ + width));
                        vertices.Add(new float3(x0, startY + height, startZ + width));
                        vertices.Add(new float3(x0, startY + height, startZ));
                        vertices.Add(new float3(x0, startY, startZ));
                    }
                }
                else if (math.abs(normal.y) > 0)
                {
                    float y0 = startY + math.max(0, normal.y);
                    if (normal.y > 0)
                    {
                        vertices.Add(new float3(startX, y0, startZ));
                        vertices.Add(new float3(startX, y0, startZ + height));
                        vertices.Add(new float3(startX + width, y0, startZ + height));
                        vertices.Add(new float3(startX + width, y0, startZ));
                    }
                    else
                    {
                        vertices.Add(new float3(startX, y0, startZ));
                        vertices.Add(new float3(startX + width, y0, startZ));
                        vertices.Add(new float3(startX + width, y0, startZ + height));
                        vertices.Add(new float3(startX, y0, startZ + height));
                    }
                }
                else
                {
                    float z0 = startZ + math.max(0, normal.z);
                    if (normal.z > 0)
                    {
                        vertices.Add(new float3(startX + width, startY, z0));
                        vertices.Add(new float3(startX + width, startY + height, z0));
                        vertices.Add(new float3(startX, startY + height, z0));
                        vertices.Add(new float3(startX, startY, z0));
                    }
                    else
                    {
                        vertices.Add(new float3(startX, startY, z0));
                        vertices.Add(new float3(startX, startY + height, z0));
                        vertices.Add(new float3(startX + width, startY + height, z0));
                        vertices.Add(new float3(startX + width, startY, z0));
                    }
                }

                uvs.Add(new float2(0, 0));
                uvs.Add(new float2(0, height));
                uvs.Add(new float2(width, height));
                uvs.Add(new float2(width, 0));

                for (int i = 0; i < 4; i++)
                {
                    colors.Add(color);
                    normals.Add(normal);
                }

                triangles.Add(vertexStart);
                triangles.Add(vertexStart + 1);
                triangles.Add(vertexStart + 2);
                triangles.Add(vertexStart);
                triangles.Add(vertexStart + 2);
                triangles.Add(vertexStart + 3);
            }

            public void Execute()
            {
                for (int y = 0; y < maxYLevel; y++)
                for (int z = 0; z < ChunkData.SIZE; z++)
                for (int x = 0; x < ChunkData.SIZE; x++)
                {
                    int idx = (y * ChunkData.SIZE * ChunkData.SIZE) + (z * ChunkData.SIZE) + x;
                    byte block = blocks[idx];
                    if (block == 0 || block >= blockColors.Length) continue;

                    // X+ face
                    if (!processedXPos[x, y, z] && IsBlockFaceVisible(x, y, z, 1, 0, 0))
                    {
                        int width = 1;
                        int height = 1;

                        while (z + width < ChunkData.SIZE && 
                               !processedXPos[x, y, z + width] &&
                               CanMergeBlocks(x, y, z, x, y, z + width) &&
                               IsBlockFaceVisible(x, y, z + width, 1, 0, 0))
                        {
                            width++;
                        }

                        bool canExpandHeight = true;
                        while (canExpandHeight && y + height < maxYLevel)
                        {
                            for (int dz = 0; dz < width; dz++)
                            {
                                if (processedXPos[x, y + height, z + dz] ||
                                    !CanMergeBlocks(x, y, z, x, y + height, z + dz) ||
                                    !IsBlockFaceVisible(x, y + height, z + dz, 1, 0, 0))
                                {
                                    canExpandHeight = false;
                                    break;
                                }
                            }
                            if (canExpandHeight) height++;
                        }

                        for (int dy = 0; dy < height; dy++)
                        for (int dz = 0; dz < width; dz++)
                        {
                            processedXPos[x, y + dy, z + dz] = true;
                        }

                        AddGreedyFace(x, y, z, width, height, new float3(1, 0, 0), block);
                    }

                    // X- face
                    if (!processedXNeg[x, y, z] && IsBlockFaceVisible(x, y, z, -1, 0, 0))
                    {
                        int width = 1;
                        int height = 1;

                        while (z + width < ChunkData.SIZE && 
                               !processedXNeg[x, y, z + width] &&
                               CanMergeBlocks(x, y, z, x, y, z + width) &&
                               IsBlockFaceVisible(x, y, z + width, -1, 0, 0))
                        {
                            width++;
                        }

                        bool canExpandHeight = true;
                        while (canExpandHeight && y + height < maxYLevel)
                        {
                            for (int dz = 0; dz < width; dz++)
                            {
                                if (processedXNeg[x, y + height, z + dz] ||
                                    !CanMergeBlocks(x, y, z, x, y + height, z + dz) ||
                                    !IsBlockFaceVisible(x, y + height, z + dz, -1, 0, 0))
                                {
                                    canExpandHeight = false;
                                    break;
                                }
                            }
                            if (canExpandHeight) height++;
                        }

                        for (int dy = 0; dy < height; dy++)
                        for (int dz = 0; dz < width; dz++)
                        {
                            processedXNeg[x, y + dy, z + dz] = true;
                        }

                        AddGreedyFace(x, y, z, width, height, new float3(-1, 0, 0), block);
                    }

                    // Y+ face
                    if (!processedYPos[x, y, z] && IsBlockFaceVisible(x, y, z, 0, 1, 0))
                    {
                        int width = 1;
                        int depth = 1;

                        while (x + width < ChunkData.SIZE && 
                               !processedYPos[x + width, y, z] &&
                               CanMergeBlocks(x, y, z, x + width, y, z) &&
                               IsBlockFaceVisible(x + width, y, z, 0, 1, 0))
                        {
                            width++;
                        }

                        bool canExpandDepth = true;
                        while (canExpandDepth && z + depth < ChunkData.SIZE)
                        {
                            for (int dx = 0; dx < width; dx++)
                            {
                                if (processedYPos[x + dx, y, z + depth] ||
                                    !CanMergeBlocks(x, y, z, x + dx, y, z + depth) ||
                                    !IsBlockFaceVisible(x + dx, y, z + depth, 0, 1, 0))
                                {
                                    canExpandDepth = false;
                                    break;
                                }
                            }
                            if (canExpandDepth) depth++;
                        }

                        for (int dx = 0; dx < width; dx++)
                        for (int dz = 0; dz < depth; dz++)
                        {
                            processedYPos[x + dx, y, z + dz] = true;
                        }

                        AddGreedyFace(x, y, z, width, depth, new float3(0, 1, 0), block);
                    }

                    // Y- face
                    if (!processedYNeg[x, y, z] && IsBlockFaceVisible(x, y, z, 0, -1, 0))
                    {
                        int width = 1;
                        int depth = 1;

                        while (x + width < ChunkData.SIZE && 
                               !processedYNeg[x + width, y, z] &&
                               CanMergeBlocks(x, y, z, x + width, y, z) &&
                               IsBlockFaceVisible(x + width, y, z, 0, -1, 0))
                        {
                            width++;
                        }

                        bool canExpandDepth = true;
                        while (canExpandDepth && z + depth < ChunkData.SIZE)
                        {
                            for (int dx = 0; dx < width; dx++)
                            {
                                if (processedYNeg[x + dx, y, z + depth] ||
                                    !CanMergeBlocks(x, y, z, x + dx, y, z + depth) ||
                                    !IsBlockFaceVisible(x + dx, y, z + depth, 0, -1, 0))
                                {
                                    canExpandDepth = false;
                                    break;
                                }
                            }
                            if (canExpandDepth) depth++;
                        }

                        for (int dx = 0; dx < width; dx++)
                        for (int dz = 0; dz < depth; dz++)
                        {
                            processedYNeg[x + dx, y, z + dz] = true;
                        }

                        AddGreedyFace(x, y, z, width, depth, new float3(0, -1, 0), block);
                    }

                    // Z+ face
                    if (!processedZPos[x, y, z] && IsBlockFaceVisible(x, y, z, 0, 0, 1))
                    {
                        int width = 1;
                        int height = 1;

                        while (x + width < ChunkData.SIZE && 
                               !processedZPos[x + width, y, z] &&
                               CanMergeBlocks(x, y, z, x + width, y, z) &&
                               IsBlockFaceVisible(x + width, y, z, 0, 0, 1))
                        {
                            width++;
                        }

                        bool canExpandHeight = true;
                        while (canExpandHeight && y + height < maxYLevel)
                        {
                            for (int dx = 0; dx < width; dx++)
                            {
                                if (processedZPos[x + dx, y + height, z] ||
                                    !CanMergeBlocks(x, y, z, x + dx, y + height, z) ||
                                    !IsBlockFaceVisible(x + dx, y + height, z, 0, 0, 1))
                                {
                                    canExpandHeight = false;
                                    break;
                                }
                            }
                            if (canExpandHeight) height++;
                        }

                        for (int dy = 0; dy < height; dy++)
                        for (int dx = 0; dx < width; dx++)
                        {
                            processedZPos[x + dx, y + dy, z] = true;
                        }

                        AddGreedyFace(x, y, z, width, height, new float3(0, 0, 1), block);
                    }

                    // Z- face
                    if (!processedZNeg[x, y, z] && IsBlockFaceVisible(x, y, z, 0, 0, -1))
                    {
                        int width = 1;
                        int height = 1;

                        while (x + width < ChunkData.SIZE && 
                               !processedZNeg[x + width, y, z] &&
                               CanMergeBlocks(x, y, z, x + width, y, z) &&
                               IsBlockFaceVisible(x + width, y, z, 0, 0, -1))
                        {
                            width++;
                        }

                        bool canExpandHeight = true;
                        while (canExpandHeight && y + height < maxYLevel)
                        {
                            for (int dx = 0; dx < width; dx++)
                            {
                                if (processedZNeg[x + dx, y + height, z] ||
                                    !CanMergeBlocks(x, y, z, x + dx, y + height, z) ||
                                    !IsBlockFaceVisible(x + dx, y + height, z, 0, 0, -1))
                                {
                                    canExpandHeight = false;
                                    break;
                                }
                            }
                            if (canExpandHeight) height++;
                        }

                        for (int dy = 0; dy < height; dy++)
                        for (int dx = 0; dx < width; dx++)
                        {
                            processedZNeg[x + dx, y + dy, z] = true;
                        }

                        AddGreedyFace(x, y, z, width, height, new float3(0, 0, -1), block);
                    }
                }
            }
        }

        public static JobHandle GenerateMesh(
            NativeArray<byte> blocks,
            NativeArray<float4> blockColors,
            int maxYLevel,
            out NativeList<float3> vertices,
            out NativeList<int> triangles,
            out NativeList<float2> uvs,
            out NativeList<float4> colors,
            out NativeList<float3> normals)
        {
            vertices = new NativeList<float3>(4096, Allocator.TempJob);
            triangles = new NativeList<int>(6144, Allocator.TempJob);
            uvs = new NativeList<float2>(4096, Allocator.TempJob);
            colors = new NativeList<float4>(4096, Allocator.TempJob);
            normals = new NativeList<float3>(4096, Allocator.TempJob);

            // Initialize the processed arrays
            var processedXPos = new NativeArray3D<bool>(ChunkData.SIZE, maxYLevel, ChunkData.SIZE, Allocator.TempJob);
            var processedXNeg = new NativeArray3D<bool>(ChunkData.SIZE, maxYLevel, ChunkData.SIZE, Allocator.TempJob);
            var processedYPos = new NativeArray3D<bool>(ChunkData.SIZE, maxYLevel, ChunkData.SIZE, Allocator.TempJob);
            var processedYNeg = new NativeArray3D<bool>(ChunkData.SIZE, maxYLevel, ChunkData.SIZE, Allocator.TempJob);
            var processedZPos = new NativeArray3D<bool>(ChunkData.SIZE, maxYLevel, ChunkData.SIZE, Allocator.TempJob);
            var processedZNeg = new NativeArray3D<bool>(ChunkData.SIZE, maxYLevel, ChunkData.SIZE, Allocator.TempJob);

            var job = new GenerateMeshJob
            {
                blocks = blocks,
                blockColors = blockColors,
                vertices = vertices,
                triangles = triangles,
                uvs = uvs,
                colors = colors,
                normals = normals,
                maxYLevel = maxYLevel,
                processedXPos = processedXPos,
                processedXNeg = processedXNeg,
                processedYPos = processedYPos,
                processedYNeg = processedYNeg,
                processedZPos = processedZPos,
                processedZNeg = processedZNeg
            };

            return job.Schedule();
        }
    }
} 