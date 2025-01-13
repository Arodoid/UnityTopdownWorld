using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using Random = UnityEngine.Random;
using EntitySystem.Core.World;

namespace EntitySystem.Core.World
{
    public class PathFinder
    {
        private readonly IWorldAccess _worldAccess;
        private const int MAX_JUMP_HEIGHT = 1;
        private const int MAX_FALL_HEIGHT = 4;
        private bool _debugMode = true;

        private class PathNode
        {
            public Vector3Int Position { get; set; }
            public PathNode Parent { get; set; }
            public float G { get; set; } // Cost from start
            public float H { get; set; } // Estimated cost to end
            public float F => G + H;     // Total cost

            public PathNode(Vector3Int pos)
            {
                Position = pos;
            }
        }

        public PathFinder(IWorldAccess worldAccess)
        {
            _worldAccess = worldAccess;
        }

        public bool IsPathPossible(Vector3 start, Vector3 end)
        {
            var path = FindPath(start, end);
            return path != null && path.Count > 0;
        }

        public List<Vector3> FindPath(Vector3 start, Vector3 end)
        {
            var startPos = GetValidGroundPosition(start);
            var endPos = GetValidGroundPosition(end);

            if (!startPos.HasValue || !endPos.HasValue)
            {
                if (_debugMode) Debug.Log($"Invalid start or end position. Start: {start}, End: {end}");
                return null;
            }

            return FindPathInternal(startPos.Value, endPos.Value);
        }

        public Vector3? FindRandomAccessiblePosition(Vector3 center, float minRadius, float maxRadius, int maxAttempts = 10)
        {
            for (int i = 0; i < maxAttempts; i++)
            {
                float angle = Random.Range(0f, 360f);
                float distance = Random.Range(minRadius, maxRadius);
                Vector3 offset = Quaternion.Euler(0, angle, 0) * Vector3.forward * distance;
                Vector3 targetPos = center + offset;

                var validPos = GetValidGroundPosition(targetPos);
                if (validPos.HasValue && IsPathPossible(center, validPos.Value))
                {
                    return validPos;
                }
            }

            if (_debugMode) Debug.Log($"Failed to find random accessible position around {center}");
            return null;
        }

        private Vector3Int? GetValidGroundPosition(Vector3 position)
        {
            var pos = Vector3Int.FloorToInt(position);
            int groundY = _worldAccess.GetHighestSolidBlock(pos.x, pos.z);
            
            if (groundY < 0) return null;

            var groundPos = new Vector3Int(pos.x, groundY + 1, pos.z);
            return HasValidHeadroom(groundPos) ? groundPos : null;
        }

        private bool HasValidHeadroom(Vector3Int pos)
        {
            return !_worldAccess.IsBlockSolid(new int3(pos.x, pos.y, pos.z)) &&
                   !_worldAccess.IsBlockSolid(new int3(pos.x, pos.y + 1, pos.z));
        }

        private List<Vector3> FindPathInternal(Vector3Int start, Vector3Int end)
        {
            var openSet = new List<PathNode>();
            var closedSet = new HashSet<Vector3Int>();
            
            var startNode = new PathNode(start);
            startNode.G = 0;
            startNode.H = Vector3Int.Distance(start, end);
            
            openSet.Add(startNode);

            while (openSet.Count > 0)
            {
                var current = openSet.OrderBy(n => n.F).First();
                
                if (current.Position == end)
                {
                    return ReconstructPath(current);
                }

                openSet.Remove(current);
                closedSet.Add(current.Position);

                foreach (var neighbor in GetNeighbors(current.Position))
                {
                    if (closedSet.Contains(neighbor))
                        continue;

                    float newG = current.G + Vector3Int.Distance(current.Position, neighbor);
                    
                    var neighborNode = openSet.FirstOrDefault(n => n.Position == neighbor);
                    if (neighborNode == null)
                    {
                        neighborNode = new PathNode(neighbor);
                        neighborNode.G = newG;
                        neighborNode.H = Vector3Int.Distance(neighbor, end);
                        neighborNode.Parent = current;
                        openSet.Add(neighborNode);
                    }
                    else if (newG < neighborNode.G)
                    {
                        neighborNode.G = newG;
                        neighborNode.Parent = current;
                    }
                }
            }

            return null;
        }

        private List<Vector3> ReconstructPath(PathNode endNode)
        {
            var path = new List<Vector3>();
            var current = endNode;

            while (current != null)
            {
                path.Add(current.Position + new Vector3(0.5f, 0, 0.5f)); // Center of block
                current = current.Parent;
            }

            path.Reverse();
            return path;
        }

        private List<Vector3Int> GetNeighbors(Vector3Int pos)
        {
            var neighbors = new List<Vector3Int>();
            
            // Add diagonal movements for more natural pathfinding
            var horizontalOffsets = new[]
            {
                new Vector3Int(1, 0, 0),
                new Vector3Int(-1, 0, 0),
                new Vector3Int(0, 0, 1),
                new Vector3Int(0, 0, -1),
                new Vector3Int(1, 0, 1),
                new Vector3Int(1, 0, -1),
                new Vector3Int(-1, 0, 1),
                new Vector3Int(-1, 0, -1)
            };

            foreach (var offset in horizontalOffsets)
            {
                var neighborPos = new Vector3Int(
                    pos.x + offset.x,
                    pos.y,
                    pos.z + offset.z
                );
                
                if (Mathf.Abs(neighborPos.x) > 1000 || Mathf.Abs(neighborPos.z) > 1000)
                    continue;

                if (_debugMode)
                {
                    Debug.Log($"Checking neighbor at {neighborPos} from position {pos}");
                }

                int groundHeight = FindGroundHeight(neighborPos);
                
                if (groundHeight < 0)
                {
                    if (_debugMode) Debug.Log($"No valid ground at {neighborPos}");
                    continue;
                }

                int heightDiff = groundHeight - pos.y;
                if (heightDiff > MAX_JUMP_HEIGHT)
                {
                    if (_debugMode) Debug.Log($"Height difference too large: {heightDiff} at {neighborPos}");
                    continue;
                }
                
                if (heightDiff < -MAX_FALL_HEIGHT)
                {
                    if (_debugMode) Debug.Log($"Drop too steep: {heightDiff} at {neighborPos}");
                    continue;
                }

                var validNeighbor = new Vector3Int(neighborPos.x, groundHeight, neighborPos.z);
                if (_debugMode) Debug.Log($"Found valid neighbor at {validNeighbor}");
                neighbors.Add(validNeighbor);
            }

            return neighbors;
        }

        private int FindGroundHeight(Vector3Int pos)
        {
            // Look in a larger vertical range around the current position
            int searchRange = MAX_JUMP_HEIGHT + MAX_FALL_HEIGHT;
            int startY = pos.y + MAX_JUMP_HEIGHT;
            int endY = pos.y - searchRange;
            
            if (_debugMode)
            {
                Debug.Log($"Searching for ground from y={startY} to y={endY} at ({pos.x}, {pos.z}) from position y={pos.y}");
            }
            
            // Search downward for ground
            for (int y = startY; y >= endY; y--)
            {
                var checkPos = new int3(pos.x, y, pos.z);
                var abovePos = new int3(pos.x, y + 1, pos.z);
                
                // Look for a solid block with air above it
                if (_worldAccess.IsBlockSolid(checkPos) && !_worldAccess.IsBlockSolid(abovePos))
                {
                    if (_debugMode)
                    {
                        Debug.Log($"Found valid ground at ({pos.x}, {y}, {pos.z})");
                    }
                    return y;
                }
            }
            
            if (_debugMode)
            {
                Debug.Log($"No valid ground found between y={startY} and y={endY} at ({pos.x}, {pos.z})");
            }
            return -1;
        }
    }
}