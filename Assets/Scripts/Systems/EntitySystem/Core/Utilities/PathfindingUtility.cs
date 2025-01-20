using Unity.Mathematics;
using System.Collections.Generic;
using WorldSystem.API;
using System;
using UnityEngine;

namespace EntitySystem.Core.Utilities
{
    public class PathfindingUtility
    {
        private readonly WorldSystemAPI _worldAPI;
        private readonly PriorityQueue<PathNode> _openSet;
        private readonly HashSet<int3> _closedSet;
        private readonly Dictionary<int3, PathNode> _allNodes;
        
        // Directions for pathfinding (including diagonals)
        private static readonly int3[] DIRECTIONS = new int3[]
        {
            new(1, 0, 0),   // right
            new(-1, 0, 0),  // left
            new(0, 0, 1),   // forward
            new(0, 0, -1),  // back
            new(1, 0, 1),   // right-forward
            new(-1, 0, 1),  // left-forward
            new(1, 0, -1),  // right-back
            new(-1, 0, -1), // left-back
            new(0, 1, 0),   // up
            new(0, -1, 0),  // down
        };

        public PathfindingUtility(WorldSystemAPI worldAPI)
        {
            _worldAPI = worldAPI;
            _openSet = new PriorityQueue<PathNode>();
            _closedSet = new HashSet<int3>();
            _allNodes = new Dictionary<int3, PathNode>();
        }

        public List<int3> FindPath(int3 start, int3 end, float entityHeight)
        {
            // Reset collections
            _openSet.Clear();
            _closedSet.Clear();
            _allNodes.Clear();

            // Create start node
            var startNode = new PathNode(start, null, 0, EstimateDistance(start, end));
            _openSet.Enqueue(startNode);
            _allNodes[start] = startNode;

            while (_openSet.Count > 0)
            {
                var current = _openSet.Dequeue();

                if (current.Position.Equals(end))
                {
                    return ReconstructPath(current);
                }

                _closedSet.Add(current.Position);

                foreach (var dir in DIRECTIONS)
                {
                    var neighborPos = current.Position + dir;
                    
                    // Skip if already evaluated
                    if (_closedSet.Contains(neighborPos)) continue;

                    // Check if position is valid for entity
                    if (!IsValidPosition(neighborPos, entityHeight)) continue;

                    // Calculate new cost
                    float moveCost = dir.y != 0 ? 1.4f : math.length(new float2(dir.x, dir.z));
                    float newCost = current.GCost + moveCost;

                    // Get or create neighbor node
                    if (!_allNodes.TryGetValue(neighborPos, out PathNode neighbor))
                    {
                        neighbor = new PathNode(
                            neighborPos,
                            current,
                            newCost,
                            EstimateDistance(neighborPos, end)
                        );
                        _allNodes[neighborPos] = neighbor;
                        _openSet.Enqueue(neighbor);
                    }
                    else if (newCost < neighbor.GCost)
                    {
                        neighbor.Parent = current;
                        neighbor.GCost = newCost;
                        _openSet.UpdatePriority(neighbor);
                    }
                }
            }

            return new List<int3>(); // No path found
        }

        public bool TryGetRandomNearbyPosition(int3 start, float maxDistance, float entityHeight, out int3 position)
        {
            const int MAX_ATTEMPTS = 10;
            position = default;

            for (int i = 0; i < MAX_ATTEMPTS; i++)
            {
                // Generate random offset within maxDistance
                int dx = UnityEngine.Random.Range(-Mathf.RoundToInt(maxDistance), Mathf.RoundToInt(maxDistance) + 1);
                int dz = UnityEngine.Random.Range(-Mathf.RoundToInt(maxDistance), Mathf.RoundToInt(maxDistance) + 1);
                
                // Find valid Y position
                int3 testPos = new int3(
                    start.x + dx,
                    start.y,
                    start.z + dz
                );

                // Check if position is within range
                if (math.length(new float2(dx, dz)) > maxDistance) continue;

                // Find ground level
                if (FindGroundPosition(testPos, entityHeight, out int3 groundPos))
                {
                    position = groundPos;
                    return true;
                }
            }

            return false;
        }

        private bool FindGroundPosition(int3 pos, float entityHeight, out int3 groundPos)
        {
            groundPos = pos;
            
            // Search down for ground
            for (int y = pos.y; y >= math.max(0, pos.y - 5); y--)
            {
                groundPos.y = y;
                if (IsValidPosition(groundPos, entityHeight))
                {
                    return true;
                }
            }

            // Search up for ground
            for (int y = pos.y + 1; y <= pos.y + 5; y++)
            {
                groundPos.y = y;
                if (IsValidPosition(groundPos, entityHeight))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsValidPosition(int3 pos, float entityHeight)
        {
            // First check if we can stand at the base position
            if (!_worldAPI.CanStandAt(pos))
            {
                return false;
            }

            // Then check height clearance (all positions above base must be non-solid)
            for (int y = 1; y < math.ceil(entityHeight); y++)
            {
                var checkPos = pos + new int3(0, y, 0);
                if (_worldAPI.IsBlockSolid(checkPos))
                {
                    return false;
                }
            }

            return true;
        }

        private float EstimateDistance(int3 a, int3 b)
        {
            var d = math.abs(b - a);
            return d.x + d.y + d.z;
        }

        private List<int3> ReconstructPath(PathNode endNode)
        {
            var path = new List<int3>();
            var current = endNode;

            while (current != null)
            {
                path.Add(current.Position);
                current = current.Parent;
            }

            path.Reverse();
            return path;
        }

        public List<int3> FindRandomPath(int3 start, float maxDistance, float entityHeight)
        {
            if (TryGetRandomNearbyPosition(start, maxDistance, entityHeight, out int3 end))
            {
                return FindPath(start, end, entityHeight);
            }
            return new List<int3>();
        }

        private class PathNode : IComparable<PathNode>
        {
            public int3 Position { get; }
            public PathNode Parent { get; set; }
            public float GCost { get; set; }  // Cost from start
            public float HCost { get; }       // Estimated cost to end
            public float FCost => GCost + HCost;

            public PathNode(int3 pos, PathNode parent, float gCost, float hCost)
            {
                Position = pos;
                Parent = parent;
                GCost = gCost;
                HCost = hCost;
            }

            public int CompareTo(PathNode other)
            {
                int comparison = FCost.CompareTo(other.FCost);
                if (comparison == 0)
                {
                    comparison = HCost.CompareTo(other.HCost);
                }
                return comparison;
            }
        }

        private class PriorityQueue<T> where T : IComparable<T>
        {
            private List<T> _data = new();

            public int Count => _data.Count;

            public void Clear() => _data.Clear();

            public void Enqueue(T item)
            {
                _data.Add(item);
                SiftUp(_data.Count - 1);
            }

            public T Dequeue()
            {
                if (_data.Count == 0) throw new InvalidOperationException();

                T result = _data[0];
                _data[0] = _data[_data.Count - 1];
                _data.RemoveAt(_data.Count - 1);

                if (_data.Count > 0)
                    SiftDown(0);

                return result;
            }

            public void UpdatePriority(T item)
            {
                int index = _data.IndexOf(item);
                if (index != -1)
                {
                    SiftUp(index);
                    SiftDown(index);
                }
            }

            private void SiftUp(int index)
            {
                while (index > 0)
                {
                    int parentIndex = (index - 1) / 2;
                    if (_data[index].CompareTo(_data[parentIndex]) >= 0)
                        break;

                    T temp = _data[index];
                    _data[index] = _data[parentIndex];
                    _data[parentIndex] = temp;
                    index = parentIndex;
                }
            }

            private void SiftDown(int index)
            {
                while (true)
                {
                    int smallest = index;
                    int leftChild = 2 * index + 1;
                    int rightChild = 2 * index + 2;

                    if (leftChild < _data.Count && _data[leftChild].CompareTo(_data[smallest]) < 0)
                        smallest = leftChild;

                    if (rightChild < _data.Count && _data[rightChild].CompareTo(_data[smallest]) < 0)
                        smallest = rightChild;

                    if (smallest == index)
                        break;

                    T temp = _data[index];
                    _data[index] = _data[smallest];
                    _data[smallest] = temp;
                    index = smallest;
                }
            }
        }
    }
} 