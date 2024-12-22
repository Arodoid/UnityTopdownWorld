using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Static mesh data definitions for cube generation.
/// Provides vertex and normal data for each cube face.
/// </summary>
public static class CubeMeshData
{
    public static readonly Vector3[] Vertices = {
        new Vector3(0, 0, 0), // 0
        new Vector3(1, 0, 0), // 1
        new Vector3(1, 1, 0), // 2
        new Vector3(0, 1, 0), // 3
        new Vector3(0, 0, 1), // 4
        new Vector3(1, 0, 1), // 5
        new Vector3(1, 1, 1), // 6
        new Vector3(0, 1, 1)  // 7
    };

    public static readonly Vector3[] Normals = {
        Vector3.up,
        Vector3.down,
        Vector3.forward,
        Vector3.back,
        Vector3.left,
        Vector3.right
    };

    public static readonly int[][] FaceTriangles = {
        new int[] { 7, 6, 2, 3 },    // Top    (+Y)
        new int[] { 0, 1, 5, 4 },    // Bottom (-Y)
        new int[] { 4, 5, 6, 7 },    // Front  (+Z)
        new int[] { 1, 0, 3, 2 },    // Back   (-Z)
        new int[] { 0, 4, 7, 3 },    // Left   (-X)
        new int[] { 5, 1, 2, 6 }     // Right  (+X)
    };

    public static readonly Vector2[] UVs = {
        new Vector2(0, 0),
        new Vector2(1, 0),
        new Vector2(1, 1),
        new Vector2(0, 1)
    };
} 