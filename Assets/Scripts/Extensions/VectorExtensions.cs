using UnityEngine;

namespace VoxelGame.Extensions
{
    public static class VectorExtensions
    {
        public static Vector3Int ToVector3Int(this Vector3 v)
        {
            return new Vector3Int(
                Mathf.RoundToInt(v.x),
                Mathf.RoundToInt(v.y),
                Mathf.RoundToInt(v.z)
            );
        }

        public static Vector3Int DirectionToVector3Int(this Vector3 v)
        {
            return new Vector3Int(
                Mathf.Clamp(Mathf.RoundToInt(v.normalized.x), -1, 1),
                Mathf.Clamp(Mathf.RoundToInt(v.normalized.y), -1, 1),
                Mathf.Clamp(Mathf.RoundToInt(v.normalized.z), -1, 1)
            );
        }
    }
} 