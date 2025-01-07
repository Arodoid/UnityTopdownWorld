using Unity.Mathematics;
using Unity.Collections;
using WorldSystem.Data;
using UnityEngine;

namespace WorldSystem.Mesh
{
    public interface IChunkMeshBuilder
    {
        void QueueMeshBuild(int2 position, NativeArray<HeightPoint> heightMap, MeshFilter meshFilter, MeshFilter shadowMeshFilter);
        bool IsBuildingMesh(int2 position);
        void Update();
        void Dispose();
    }
} 