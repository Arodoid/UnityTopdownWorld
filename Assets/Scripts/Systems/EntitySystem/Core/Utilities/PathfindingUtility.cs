using UnityEngine;
using Unity.Mathematics;
using System;
using System.Collections.Generic;
using WorldSystem.API;
using Random = UnityEngine.Random;
using System.Linq;

namespace EntitySystem.Core.Utilities
{
    public class PathfindingUtility
    {
        private readonly WorldSystemAPI _worldAPI;
        private const int MAX_CLIMB = 1;  // Can climb 1 block
        private const int MAX_DROP = 3;   // Can drop 3 blocks

        public PathfindingUtility(WorldSystemAPI worldAPI)
        {
            _worldAPI = worldAPI;
        }

        public List<Vector3> FindPath(int3 start, int3 end, float entityHeight)
        {
            var openSet = new PriorityQueue<PathNode>();
            var closedSet = new HashSet<int3>();
            var cameFrom = new Dictionary<int3, int3>();
            var gScore = new Dictionary<int3, float>();
            var fScore = new Dictionary<int3, float>();

            var startNode = new PathNode(start, 0, EstimateDistance(start, end));
            openSet.Enqueue(startNode);
            gScore[start] = 0;
            fScore[start] = startNode.FCost;

            while (openSet.Count > 0)
            {
                var current = openSet.Dequeue();

                if (current.Position.Equals(end))
                {
                    return ReconstructPath(cameFrom, end);
                }

                closedSet.Add(current.Position);

                foreach (var neighbor in GetValidNeighbors(current.Position, entityHeight))
                {
                    if (closedSet.Contains(neighbor))
                        continue;

                    float tentativeGScore = gScore[current.Position] + 
                        GetMovementCost(current.Position, neighbor);

                    if (!gScore.ContainsKey(neighbor) || tentativeGScore < gScore[neighbor])
                    {
                        cameFrom[neighbor] = current.Position;
                        gScore[neighbor] = tentativeGScore;
                        float f = tentativeGScore + EstimateDistance(neighbor, end);
                        fScore[neighbor] = f;

                        var node = new PathNode(neighbor, tentativeGScore, f);
                        openSet.Enqueue(node);
                    }
                }
            }

            return new List<Vector3>(); // No path found
        }

        private List<int3> GetValidNeighbors(int3 pos, float entityHeight)
        {
            var neighbors = new List<int3>();
            
            // Check all horizontal neighbors
            for (int x = -1; x <= 1; x++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    if (x == 0 && z == 0) continue;

                    var basePos = new int3(pos.x + x, pos.y, pos.z + z);
                    
                    // Check all possible Y levels within climb/drop range
                    for (int y = -MAX_DROP; y <= MAX_CLIMB; y++)
                    {
                        var checkPos = new int3(basePos.x, pos.y + y, basePos.z);
                        if (IsPositionValid(checkPos, entityHeight))
                        {
                            neighbors.Add(checkPos);
                        }
                    }
                }
            }

            Debug.Log($"Found {neighbors.Count} valid neighbors for position {pos}");
            return neighbors;
        }

        private bool IsPositionValid(int3 pos, float entityHeight)
        {
            // IMPORTANT: Only allow positions that are directly on top of blocks
            
            // The position must be air (where we're standing)
            if (_worldAPI.IsBlockSolid(pos))
            {
                return false;
            }

            // Must have solid block below (what we're standing on)
            if (!_worldAPI.IsBlockSolid(new int3(pos.x, pos.y - 1, pos.z)))
            {
                return false;
            }

            // Check the space above for head clearance
            for (int h = 1; h < entityHeight; h++)
            {
                if (_worldAPI.IsBlockSolid(new int3(pos.x, pos.y + h, pos.z)))
                {
                    return false;
                }
            }

            return true;
        }

        private float GetMovementCost(int3 from, int3 to)
        {
            float baseCost = math.distance(new float3(from), new float3(to));
            
            // Additional cost for vertical movement
            int heightDiff = to.y - from.y;
            if (heightDiff > 0)
                baseCost *= 1.5f; // Climbing is harder
            else if (heightDiff < 0)
                baseCost *= 1.2f; // Dropping has small penalty

            return baseCost;
        }

        private float EstimateDistance(int3 from, int3 to)
        {
            return math.distance(new float3(from), new float3(to));
        }

        private List<Vector3> ReconstructPath(Dictionary<int3, int3> cameFrom, int3 current)
        {
            var blockPath = new List<int3> { current };
            while (cameFrom.ContainsKey(current))
            {
                current = cameFrom[current];
                blockPath.Add(current);
            }
            blockPath.Reverse();
            
            // Convert block positions to world positions (centered in blocks)
            return blockPath.Select(pos => new Vector3(
                pos.x + 0.5f,  // Center in block X
                pos.y,         // Bottom of block Y
                pos.z + 0.5f   // Center in block Z
            )).ToList();
        }

        public List<Vector3> FindRandomPath(int3 start, float maxDistance, float entityHeight)
        {
            if (TryGetRandomNearbyPosition(start, maxDistance, entityHeight, out int3 end))
            {
                return FindPath(start, end, entityHeight);
            }
            return new List<Vector3>();
        }

        public bool TryGetRandomNearbyPosition(int3 start, float maxDistance, float entityHeight, out int3 position)
        {
            Debug.Log($"TryGetRandomNearbyPosition called with maxDistance: {maxDistance}");
            
            for (int attempts = 0; attempts < 30; attempts++)
            {
                float angle = Random.Range(0f, 2f * Mathf.PI);
                float distance = Random.Range(maxDistance * 0.3f, maxDistance);
                
                float xOffset = math.cos(angle) * distance;
                float zOffset = math.sin(angle) * distance;
                
                int x = start.x + (int)xOffset;
                int z = start.z + (int)zOffset;
                
                Debug.Log($"Attempt {attempts}: Trying position at distance {distance} (x:{x}, z:{z})");
                
                // Search in a larger vertical range
                int verticalRange = math.max(MAX_CLIMB, MAX_DROP);
                for (int y = -verticalRange; y <= verticalRange; y++)
                {
                    position = new int3(x, start.y + y, z);
                    
                    if (IsPositionValid(position, entityHeight))
                    {
                        // Verify the position is actually reachable
                        var path = FindPath(start, position, entityHeight);
                        if (path.Count > 0)
                        {
                            return true;
                        }
                    }
                }
            }

            position = default;
            return false;
        }

        private class PathNode : IComparable<PathNode>
        {
            public int3 Position { get; }
            public float GCost { get; }
            public float FCost { get; }

            public PathNode(int3 pos, float g, float f)
            {
                Position = pos;
                GCost = g;
                FCost = f;
            }

            public int CompareTo(PathNode other)
            {
                int compare = FCost.CompareTo(other.FCost);
                if (compare == 0)
                {
                    compare = GCost.CompareTo(other.GCost);
                }
                return compare;
            }
        }

        private class PriorityQueue<T> where T : IComparable<T>
        {
            private List<T> _list = new();

            public int Count => _list.Count;

            public void Enqueue(T item)
            {
                _list.Add(item);
                int ci = _list.Count - 1;
                while (ci > 0)
                {
                    int pi = (ci - 1) / 2;
                    if (_list[ci].CompareTo(_list[pi]) >= 0)
                        break;
                    T tmp = _list[ci];
                    _list[ci] = _list[pi];
                    _list[pi] = tmp;
                    ci = pi;
                }
            }

            public T Dequeue()
            {
                int li = _list.Count - 1;
                T frontItem = _list[0];
                _list[0] = _list[li];
                _list.RemoveAt(li);

                --li;
                int pi = 0;
                while (true)
                {
                    int ci = pi * 2 + 1;
                    if (ci > li) break;
                    int rc = ci + 1;
                    if (rc <= li && _list[rc].CompareTo(_list[ci]) < 0)
                        ci = rc;
                    if (_list[pi].CompareTo(_list[ci]) <= 0)
                        break;
                    T tmp = _list[pi];
                    _list[pi] = _list[ci];
                    _list[ci] = tmp;
                    pi = ci;
                }
                return frontItem;
            }
        }
    }
}