using UnityEngine;
using System.Collections.Generic;

public static class CubeMeshData
{
    public static readonly Vector3[] TopFace = new Vector3[]
    {
        new Vector3(0, 1, 0),
        new Vector3(0, 1, 1),
        new Vector3(1, 1, 1),
        new Vector3(1, 1, 0)
    };

    public static readonly Vector3[] FrontFace = new Vector3[]
    {
        new Vector3(0, 0, 1),
        new Vector3(1, 0, 1),
        new Vector3(1, 1, 1),
        new Vector3(0, 1, 1)
    };

    public static readonly Vector3[] BackFace = new Vector3[]
    {
        new Vector3(1, 0, 0),
        new Vector3(0, 0, 0),
        new Vector3(0, 1, 0),
        new Vector3(1, 1, 0)
    };

    public static readonly Vector3[] LeftFace = new Vector3[]
    {
        new Vector3(0, 0, 0),
        new Vector3(0, 0, 1),
        new Vector3(0, 1, 1),
        new Vector3(0, 1, 0)
    };

    public static readonly Vector3[] RightFace = new Vector3[]
    {
        new Vector3(1, 0, 1),
        new Vector3(1, 0, 0),
        new Vector3(1, 1, 0),
        new Vector3(1, 1, 1)
    };

    // Dictionary to map face direction to normal
    public static readonly Dictionary<Vector3[], Vector3> FaceNormals = new Dictionary<Vector3[], Vector3>
    {
        { TopFace, Vector3.up },
        { FrontFace, Vector3.forward },
        { BackFace, Vector3.back },
        { LeftFace, Vector3.left },
        { RightFace, Vector3.right }
    };
} 