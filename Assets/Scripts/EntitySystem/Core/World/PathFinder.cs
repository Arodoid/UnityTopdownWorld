using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using Random = UnityEngine.Random;
using WorldSystem;

namespace EntitySystem.Core.World
{
    public class PathFinder
    {
        private readonly IWorldSystem _worldSystem;
        private const int MAX_JUMP_HEIGHT = 1;
        private const int MAX_FALL_HEIGHT = 4;
        private const int MAX_ITERATIONS = 1000;
        private const int MAX_SEARCH_DISTANCE = 32;
        private bool _debugMode = false;
        private const float DIAGONAL_COST = 1.4f;
        
        // Debug settings
        private const float DEBUG_PATH_DURATION = 3f;
        private static readonly Color PATH_COLOR = new Color(0, 1, 0, 0.8f);    // Green
        private static readonly Color ATTEMPT_COLOR = new Color(1, 0, 0, 0.3f); // Red
        private static readonly Color SUCCESS_COLOR = new Color(0, 1, 0, 0.3f); // Green
        private static readonly Color START_COLOR = Color.blue;
        private static readonly Color END_COLOR = Color.yellow;

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

        public PathFinder(IWorldSystem worldSystem)
        {
            _worldSystem = worldSystem;
        }

        public bool IsPathPossible(Vector3 start, Vector3 end)
        {
            var path = FindPath(start, end);
            return path != null && path.Count > 0;
        }

        public List<Vector3> FindPath(Vector3 start, Vector3 end)
        {
            if (_debugMode)
            {
                // Draw start and end points
                Debug.DrawLine(start, start + Vector3.up * 2f, START_COLOR, DEBUG_PATH_DURATION);
                Debug.DrawLine(end, end + Vector3.up * 2f, END_COLOR, DEBUG_PATH_DURATION);
                DebugDrawCube(start, 0.5f, START_COLOR);
                DebugDrawCube(end, 0.5f, END_COLOR);
            }

            var startPos = ValidatePosition(start);
            var endPos = ValidatePosition(end);

            if (!startPos.HasValue || !endPos.HasValue)
            {
                return null;
            }

            // Quick check before attempting expensive pathfinding
            if (!IsPathPossibleQuick(startPos.Value, endPos.Value))
            {
                if (_debugMode) Debug.Log($"Path rejected early: distance={Vector3Int.Distance(startPos.Value, endPos.Value)}, heightDiff={Mathf.Abs(endPos.Value.y - startPos.Value.y)}");
                return null;
            }

            return FindPathInternal(startPos.Value, endPos.Value);
        }

        public List<Vector3> FindRandomAccessiblePosition(Vector3 startPos, float minRadius, float maxRadius)
        {
            // Try a few different angles for better distribution
            float[] angles = { 0, 45, 90, 135, 180, 225, 270, 315 };
            angles = angles.OrderBy(x => Random.value).ToArray(); // Shuffle angles

            foreach (float angle in angles)
            {
                // Try different distances from min to max
                float distance = Random.Range(minRadius, maxRadius);
                
                // Calculate target using angle and distance
                float radian = angle * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(
                    Mathf.Cos(radian) * distance,
                    0,
                    Mathf.Sin(radian) * distance
                );
                
                Vector3 targetPos = startPos + offset;
                
                // Find ground height at target
                int groundY = _worldSystem.GetHighestSolidBlock(
                    Mathf.RoundToInt(targetPos.x),
                    Mathf.RoundToInt(targetPos.z)
                );

                if (groundY >= 0)
                {
                    targetPos.y = groundY + 1; // One block above ground
                    
                    // Check if we can actually get there
                    var path = FindPath(startPos, targetPos);
                    if (path != null && path.Count > 0)
                    {
                        return path;
                    }
                }
            }

            // If we couldn't find a path in any direction, try closer
            if (minRadius > 2)
            {
                return FindRandomAccessiblePosition(startPos, 2, minRadius);
            }

            return null;
        }

        private Vector3Int? ValidatePosition(Vector3 position)
        {

            // Round to nearest block position instead of floor
            var posInt = new Vector3Int(
                Mathf.RoundToInt(position.x),
                Mathf.RoundToInt(position.y),
                Mathf.RoundToInt(position.z)
            );

            // Find the ground position
            var groundPos = GetValidGroundPosition(posInt);
            
            
            return groundPos.HasValue ? Vector3Int.RoundToInt(groundPos.Value) : null;
        }

        private Vector3? GetValidGroundPosition(Vector3Int pos)
        {

            // Get the actual ground height
            int groundHeight = _worldSystem.GetHighestSolidBlock(pos.x, pos.z);
            
            if (groundHeight < 0)
            {
                if (_debugMode)
                    Debug.Log($"No ground found at: ({pos.x}, {pos.z})");
                return null;
            }

            // Position should be one block above ground
            var standingPos = new Vector3Int(pos.x, groundHeight + 1, pos.z);
            
            // Verify we can actually stand here
            if (!_worldSystem.CanStandAt(new int3(standingPos.x, standingPos.y, standingPos.z)))
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
            return !_worldSystem.IsBlockSolid(new int3(pos.x, pos.y, pos.z)) &&
                   !_worldSystem.IsBlockSolid(new int3(pos.x, pos.y + 1, pos.z));
        }

        public void EnableDebugMode(bool enabled)
        {
            _debugMode = enabled;
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

                // Draw attempted paths during search
                if (_debugMode)
                {
                    foreach (var node in openSet)
                    {
                        if (node.Parent != null)
                        {
                            Vector3 from = ToWorldPosition(node.Parent.Position);
                            Vector3 to = ToWorldPosition(node.Position);
                            Debug.DrawLine(from, to, ATTEMPT_COLOR, DEBUG_PATH_DURATION);
                            DebugDrawCube(to, 0.2f, ATTEMPT_COLOR);
                        }
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
            
            // Check closer neighbors first
            var horizontalOffsets = new[]
            {
                // Cardinal directions first (less expensive)
                new Vector3Int(0, 0, 1),   // Forward
                new Vector3Int(0, 0, -1),  // Back
                new Vector3Int(1, 0, 0),   // Right
                new Vector3Int(-1, 0, 0),  // Left
                
                // Diagonals last (more expensive)
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

                // Start from current height and work down
                // This prioritizes flatter paths
                for (int y = pos.y; y >= pos.y - MAX_FALL_HEIGHT; y--)
                {
                    var testPos = new Vector3Int(neighborPos.x, y, neighborPos.z);
                    
                    if (_worldSystem.CanStandAt(new int3(testPos.x, testPos.y, testPos.z)) &&
                        HasValidHeadroom(testPos))
                    {
                        neighbors.Add(testPos);
                        break; // Found valid position, stop checking lower heights
                    }
                }

                // Only check upward if we haven't found a valid position
                if (!neighbors.Contains(neighborPos))
                {
                    for (int y = pos.y + 1; y <= pos.y + MAX_JUMP_HEIGHT; y++)
                    {
                        var testPos = new Vector3Int(neighborPos.x, y, neighborPos.z);
                        
                        if (_worldSystem.CanStandAt(new int3(testPos.x, testPos.y, testPos.z)) &&
                            HasValidHeadroom(testPos))
                        {
                            neighbors.Add(testPos);
                            break;
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
                if (_worldSystem.IsBlockSolid(checkPos) && !_worldSystem.IsBlockSolid(abovePos))
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

        private void DrawPath(List<Vector3> path)
        {
            if (path == null || path.Count < 2) return;

            // Draw path segments
            for (int i = 0; i < path.Count - 1; i++)
            {
                Vector3 from = path[i];
                Vector3 to = path[i + 1];
                
                // Draw line segment
                Debug.DrawLine(from, to, PATH_COLOR, DEBUG_PATH_DURATION);
                
                // Draw waypoint marker
                DebugDrawCube(from, 0.3f, PATH_COLOR);
            }
            
            // Draw final waypoint
            DebugDrawCube(path[path.Count - 1], 0.3f, PATH_COLOR);
        }

        private Vector3 ToWorldPosition(Vector3Int pos)
        {
            return new Vector3(pos.x + 0.5f, pos.y + 0.5f, pos.z + 0.5f);
        }

        private void DebugDrawCube(Vector3 position, float size, Color color)
        {
            Vector3 halfSize = Vector3.one * size * 0.5f;
            
            // Bottom face
            Debug.DrawLine(position - halfSize, position + new Vector3(size, -halfSize.y, -halfSize.z), color, DEBUG_PATH_DURATION);
            Debug.DrawLine(position + new Vector3(halfSize.x, -halfSize.y, -halfSize.z), position + new Vector3(halfSize.x, -halfSize.y, halfSize.z), color, DEBUG_PATH_DURATION);
            Debug.DrawLine(position + new Vector3(halfSize.x, -halfSize.y, halfSize.z), position + new Vector3(-halfSize.x, -halfSize.y, halfSize.z), color, DEBUG_PATH_DURATION);
            Debug.DrawLine(position + new Vector3(-halfSize.x, -halfSize.y, halfSize.z), position - halfSize, color, DEBUG_PATH_DURATION);
            
            // Top face
            Debug.DrawLine(position + new Vector3(-halfSize.x, halfSize.y, -halfSize.z), position + new Vector3(halfSize.x, halfSize.y, -halfSize.z), color, DEBUG_PATH_DURATION);
            Debug.DrawLine(position + new Vector3(halfSize.x, halfSize.y, -halfSize.z), position + new Vector3(halfSize.x, halfSize.y, halfSize.z), color, DEBUG_PATH_DURATION);
            Debug.DrawLine(position + new Vector3(halfSize.x, halfSize.y, halfSize.z), position + new Vector3(-halfSize.x, halfSize.y, halfSize.z), color, DEBUG_PATH_DURATION);
            Debug.DrawLine(position + new Vector3(-halfSize.x, halfSize.y, halfSize.z), position + new Vector3(-halfSize.x, halfSize.y, -halfSize.z), color, DEBUG_PATH_DURATION);
            
            // Vertical edges
            Debug.DrawLine(position - halfSize, position + new Vector3(-halfSize.x, halfSize.y, -halfSize.z), color, DEBUG_PATH_DURATION);
            Debug.DrawLine(position + new Vector3(halfSize.x, -halfSize.y, -halfSize.z), position + new Vector3(halfSize.x, halfSize.y, -halfSize.z), color, DEBUG_PATH_DURATION);
            Debug.DrawLine(position + new Vector3(halfSize.x, -halfSize.y, halfSize.z), position + new Vector3(halfSize.x, halfSize.y, halfSize.z), color, DEBUG_PATH_DURATION);
            Debug.DrawLine(position + new Vector3(-halfSize.x, -halfSize.y, halfSize.z), position + new Vector3(-halfSize.x, halfSize.y, halfSize.z), color, DEBUG_PATH_DURATION);
        }

        // Add early exit for impossible paths
        private bool IsPathPossibleQuick(Vector3Int start, Vector3Int end)
        {
            // If too far apart, don't even try
            if (Vector3Int.Distance(start, end) > MAX_SEARCH_DISTANCE)
                return false;

            // If height difference is too extreme
            int heightDiff = Mathf.Abs(end.y - start.y);
            if (heightDiff > MAX_JUMP_HEIGHT + MAX_FALL_HEIGHT)
                return false;

            return true;
        }
    }
}