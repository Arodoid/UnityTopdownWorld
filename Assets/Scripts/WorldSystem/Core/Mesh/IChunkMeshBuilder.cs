using Unity.Mathematics;
using Unity.Collections;
using WorldSystem.Data;
using UnityEngine;

namespace WorldSystem.Mesh
{
    public interface IChunkMeshBuilder
    {
        void QueueMeshBuild(int2 position, NativeArray<byte> blocks, MeshFilter meshFilter, 
            MeshFilter shadowMeshFilter);
        void QueueMeshBuild(int2 position, NativeArray<byte> blocks, MeshFilter meshFilter, 
            MeshFilter shadowMeshFilter, int maxYLevel);
        bool IsBuildingMesh(int2 position);
        void Update();
        void Dispose();
    }
} 