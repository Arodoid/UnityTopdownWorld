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
        private const int MAX_ITERATIONS = 200;
        private const int MAX_SEARCH_DISTANCE = 32;
        private bool _debugMode = false;
        private const float DIAGONAL_COST = 1.4f;

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
            var startPos = ValidatePosition(start);
            var endPos = ValidatePosition(end);

            if (!startPos.HasValue || !endPos.HasValue) return null;

            return FindPathInternal(startPos.Value, endPos.Value);
        }

        public Vector3? FindRandomAccessiblePosition(Vector3 center, float minRadius, float maxRadius, int maxAttempts = 10)
        {
            var validCenterPos = ValidatePosition(center);
            if (!validCenterPos.HasValue) return null;

            for (int i = 0; i < maxAttempts; i++)
            {
                // Generate random position
                float angle = Random.Range(0f, 360f);
                float distance = Random.Range(minRadius, maxRadius);
                Vector3 offset = Quaternion.Euler(0, angle, 0) * Vector3.forward * distance;
                Vector3 targetPos = center + offset;

                var validTargetPos = ValidatePosition(targetPos);
                if (!validTargetPos.HasValue) continue;

                // Try to find a path between the positions
                var path = FindPathInternal(validCenterPos.Value, validTargetPos.Value);
                if (path != null && path.Count > 0)
                {
                    // Return the world position (center of block)
                    return validTargetPos.Value + new Vector3(0.5f, 0, 0.5f);
                }
            }

            return null;
        }

        private Vector3Int? ValidatePosition(Vector3 position)
        {
            // Debug the incoming position
            if (_debugMode)
                Debug.Log($"Validating position: {position}");

            // Round to nearest block position instead of floor
            var posInt = new Vector3Int(
                Mathf.RoundToInt(position.x),
                Mathf.RoundToInt(position.y),
                Mathf.RoundToInt(position.z)
            );

            // Find the ground position
            var groundPos = GetValidGroundPosition(posInt);
            
            if (_debugMode && !groundPos.HasValue)
                Debug.Log($"No valid ground found for position: {posInt}");
            
            return groundPos.HasValue ? Vector3Int.RoundToInt(groundPos.Value) : null;
        }

        private Vector3? GetValidGroundPosition(Vector3Int pos)
        {
            if (_debugMode)
                Debug.Log($"Finding ground at: {pos}");

            // Get the actual ground height
            int groundHeight = _worldAccess.GetHighestSolidBlock(pos.x, pos.z);
            
            if (groundHeight < 0)
            {
                if (_debugMode)
                    Debug.Log($"No ground found at: ({pos.x}, {pos.z})");
                return null;
            }

            // Position should be one block above ground
            var standingPos = new Vector3Int(pos.x, groundHeight + 1, pos.z);
            
            // Verify we can actually stand here
            if (!_worldAccess.CanStandAt(new int3(standingPos.x, standingPos.y, standingPos.z)))
            {
                if (_debugMode)
                    Debug.Log($"Cannot stand at: {standingPos}");
                return null;
            }

            return standingPos;
        }

        private void DrawDebugCube(Vector3 center, Vector3 size, Color color, float duration)
        {
            Vector3 halfSize = size * 0.5f;
            
            // Bottom
            Vector3 p1 = center + new Vector3(-halfSize.x, -halfSize.y, -halfSize.z);
            Vector3 p2 = center + new Vector3(halfSize.x, -halfSize.y, -halfSize.z);
            Vector3 p3 = center + new Vector3(halfSize.x, -halfSize.y, halfSize.z);
            Vector3 p4 = center + new Vector3(-halfSize.x, -halfSize.y, halfSize.z);
            
            // Top
            Vector3 p5 = center + new Vector3(-halfSize.x, halfSize.y, -halfSize.z);
            Vector3 p6 = center + new Vector3(halfSize.x, halfSize.y, -halfSize.z);
            Vector3 p7 = center + new Vector3(halfSize.x, halfSize.y, halfSize.z);
            Vector3 p8 = center + new Vector3(-halfSize.x, halfSize.y, halfSize.z);
            
            // Draw bottom square
            Debug.DrawLine(p1, p2, color, duration);
            Debug.DrawLine(p2, p3, color, duration);
            Debug.DrawLine(p3, p4, color, duration);
            Debug.DrawLine(p4, p1, color, duration);
            
            // Draw top square
            Debug.DrawLine(p5, p6, color, duration);
            Debug.DrawLine(p6, p7, color, duration);
            Debug.DrawLine(p7, p8, color, duration);
            Debug.DrawLine(p8, p5, color, duration);
            
            // Draw vertical lines
            Debug.DrawLine(p1, p5, color, duration);
            Debug.DrawLine(p2, p6, color, duration);
            Debug.DrawLine(p3, p7, color, duration);
            Debug.DrawLine(p4, p8, color, duration);
        }

        private bool HasValidHeadroom(Vector3Int pos)
        {
            return !_worldAccess.IsBlockSolid(new int3(pos.x, pos.y, pos.z)) &&
                   !_worldAccess.IsBlockSolid(new int3(pos.x, pos.y + 1, pos.z));
        }

        public void EnableDebugMode(bool enable)
        {
            _debugMode = enable;
        }

        private List<Vector3> FindPathInternal(Vector3Int start, Vector3Int end)
        {
            // This method now assumes positions are already validated
            if (Vector3Int.Distance(start, end) > MAX_SEARCH_DISTANCE)
            {
                if (_debugMode) Debug.LogWarning($"Target too far: {Vector3Int.Distance(start, end)} > {MAX_SEARCH_DISTANCE}");
                return null;
            }

            var openSet = new List<PathNode>();
            var closedSet = new HashSet<Vector3Int>();
            var nodeMap = new Dictionary<Vector3Int, PathNode>();
            int iterations = 0;
            
            var startNode = new PathNode(start);
            startNode.G = 0;
            startNode.H = GetHeuristic(start, end);
            
            openSet.Add(startNode);
            nodeMap[start] = startNode;

            while (openSet.Count > 0 && iterations < MAX_ITERATIONS)
            {
                iterations++;
                var current = openSet.OrderBy(n => n.F).First();
                
                if (current.Position == end)
                {
                    return ReconstructPath(current);
                }

                openSet.Remove(current);
                closedSet.Add(current.Position);

                foreach (var neighborPos in GetNeighbors(current.Position))
                {
                    if (closedSet.Contains(neighborPos))
                        continue;

                    float moveCost = Vector3Int.Distance(current.Position, neighborPos);
                    // Apply diagonal cost if moving diagonally
                    if (neighborPos.x != current.Position.x && neighborPos.z != current.Position.z)
                    {
                        moveCost *= DIAGONAL_COST;
                    }
                    
                    float newG = current.G + moveCost;

                    PathNode neighborNode;
                    if (!nodeMap.TryGetValue(neighborPos, out neighborNode))
                    {
                        neighborNode = new PathNode(neighborPos);
                        nodeMap[neighborPos] = neighborNode;
                    }

                    if (!openSet.Contains(neighborNode))
                    {
                        neighborNode.G = newG;
                        neighborNode.H = GetHeuristic(neighborPos, end);
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

            if (iterations >= MAX_ITERATIONS)
            {
                Debug.LogWarning($"Pathfinding aborted after {MAX_ITERATIONS} iterations");
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
            
            if (Mathf.Abs(pos.x) > MAX_SEARCH_DISTANCE || Mathf.Abs(pos.z) > MAX_SEARCH_DISTANCE)
            {
                return neighbors;
            }

            // Include diagonal movements
            var horizontalOffsets = new[]
            {
                new Vector3Int(1, 0, 0),   // Right
                new Vector3Int(-1, 0, 0),  // Left
                new Vector3Int(0, 0, 1),   // Forward
                new Vector3Int(0, 0, -1),  // Back
                new Vector3Int(1, 0, 1),   // Right-Forward
                new Vector3Int(1, 0, -1),  // Right-Back
                new Vector3Int(-1, 0, 1),  // Left-Forward
                new Vector3Int(-1, 0, -1)  // Left-Back
            };

            foreach (var offset in horizontalOffsets)
            {
                var neighborPos = pos + offset;

                // Skip if out of bounds
                if (Mathf.Abs(neighborPos.x) > MAX_SEARCH_DISTANCE || 
                    Mathf.Abs(neighborPos.z) > MAX_SEARCH_DISTANCE)
                    continue;

                // Check for valid ground within acceptable height range
                for (int y = pos.y + MAX_JUMP_HEIGHT; y >= pos.y - MAX_FALL_HEIGHT; y--)
                {
                    var testPos = new Vector3Int(neighborPos.x, y, neighborPos.z);
                    
                    // Check if we can stand here
                    if (_worldAccess.CanStandAt(new int3(testPos.x, testPos.y, testPos.z)))
                    {
                        // Verify headroom
                        if (HasValidHeadroom(testPos))
                        {
                            neighbors.Add(testPos);
                            break; // Found valid position, stop checking heights
                        }
                    }
                }
            }

            return neighbors;
        }

        private int FindGroundHeight(Vector3Int pos)
        {
            // Limit vertical search range
            int searchRange = Mathf.Min(MAX_JUMP_HEIGHT + MAX_FALL_HEIGHT, 10);
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

        private void DebugDrawBlock(Vector3Int pos)
        {
            // Draw edges of the block
            Vector3 min = new Vector3(pos.x, pos.y, pos.z);
            Vector3 max = min + Vector3.one;
            
            // Bottom square
            Debug.DrawLine(new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z), Color.yellow, 5f);
            Debug.DrawLine(new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z), Color.yellow, 5f);
            Debug.DrawLine(new Vector3(max.x, min.y, max.z), new Vector3(min.x, min.y, max.z), Color.yellow, 5f);
            Debug.DrawLine(new Vector3(min.x, min.y, max.z), new Vector3(min.x, min.y, min.z), Color.yellow, 5f);
            
            // Top square
            Debug.DrawLine(new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z), Color.yellow, 5f);
            Debug.DrawLine(new Vector3(max.x, max.y, min.z), new Vector3(max.x, max.y, max.z), Color.yellow, 5f);
            Debug.DrawLine(new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z), Color.yellow, 5f);
            Debug.DrawLine(new Vector3(min.x, max.y, max.z), new Vector3(min.x, max.y, min.z), Color.yellow, 5f);
            
            // Vertical edges
            Debug.DrawLine(new Vector3(min.x, min.y, min.z), new Vector3(min.x, max.y, min.z), Color.yellow, 5f);
            Debug.DrawLine(new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z), Color.yellow, 5f);
            Debug.DrawLine(new Vector3(max.x, min.y, max.z), new Vector3(max.x, max.y, max.z), Color.yellow, 5f);
            Debug.DrawLine(new Vector3(min.x, min.y, max.z), new Vector3(min.x, max.y, max.z), Color.yellow, 5f);
        }

        private float GetHeuristic(Vector3Int start, Vector3Int end)
        {
            // Manhattan distance with height penalty
            float dx = Mathf.Abs(end.x - start.x);
            float dz = Mathf.Abs(end.z - start.z);
            float dy = Mathf.Abs(end.y - start.y);
            
            // Apply height penalty
            return dx + dz + (dy * 2f);
        }
    }
}