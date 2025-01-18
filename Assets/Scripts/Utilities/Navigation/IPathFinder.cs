using UnityEngine;
using System.Collections.Generic;

namespace Utilities.Navigation
{
    public interface IPathFinder
    {
        List<Vector3> FindPath(Vector3 start, Vector3 end);
        List<Vector3> FindRandomAccessiblePosition(Vector3 start, float minRadius, float maxRadius);
        bool IsPathPossible(Vector3 start, Vector3 end);
    }
} 