using UnityEngine;
using Unity.Mathematics;
using WorldSystem.API;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Threading.Tasks;

namespace EntitySystem.Core.Utilities
{
    public static class ChunkHelper
    {
        public static int3 GetChunkPos(int3 worldPos)
        {
            return new int3(
                worldPos.x >> 5,  // Divide by CHUNK_SIZE (32)
                worldPos.y >> 5,
                worldPos.z >> 5
            );
        }
    }

    public class PathNode
    {
        public int3 Position;
        public PathNode Parent;
        public float GCost;  // Distance from start
        public float HCost;  // Estimated distance to goal
        public float FCost => GCost + HCost;

        public PathNode(int3 pos)
        {
            Position = pos;
        }
    }

    public class Path
    {
        public List<int3> Points { get; private set; }
        public HashSet<int3> ChunksUsed { get; private set; }
        public bool IsValid { get; set; } = true;

        public Path(List<int3> points)
        {
            Points = points;
            ChunksUsed = new HashSet<int3>();
            foreach (var point in points)
            {
                ChunksUsed.Add(ChunkHelper.GetChunkPos(point));
            }
        }
    }

    public class PathfindingUtility
    {
        public WorldSystemAPI WorldAPI => _worldAPI;
        private readonly WorldSystemAPI _worldAPI;
        private const int MAX_CLIMB = 1;
        private const float DIAGONAL_COST = 1.4f;
        private const int MAX_PATH_LENGTH = 1000;
        private const int MAX_FALL_DISTANCE = 50;
        private const int MAX_CONCURRENT_PATHFINDS = 4;
        private const int MAX_QUEUED_REQUESTS = 1000;

        // Cache of active paths and their users
        private Dictionary<Entity, Path> _activePaths = new();
        private Dictionary<int3, HashSet<Entity>> _chunkPathUsers = new();

        // Reusable collections for pathfinding
        private Dictionary<int3, PathNode> _openNodes = new();
        private HashSet<int3> _closedNodes = new();
        private List<int3> _neighbors = new();

        private Queue<PathRequest> _pathRequests = new();
        private HashSet<Entity> _activePathfinding = new();
        private Dictionary<Entity, float> _lastPathRequestTime = new();
        private const float PATH_REQUEST_COOLDOWN = 0.1f; // seconds
        
        private class PathRequest
        {
            public Entity Entity;
            public int3 Start;
            public int3 End;
            public float EntityHeight;
            public Action<Path> Callback;
            public float RequestTime;
        }

        private readonly EntityManager _entityManager;

        public PathfindingUtility(WorldSystemAPI worldAPI, EntityManager entityManager)
        {
            _worldAPI = worldAPI;
            _entityManager = entityManager;
        }

        public void RequestPath(Entity entity, int3 start, int3 end, float entityHeight, Action<Path> callback)
        {
            // Prevent spam requests from same entity
            if (_lastPathRequestTime.TryGetValue(entity, out float lastRequest))
            {
                if (Time.time - lastRequest < PATH_REQUEST_COOLDOWN)
                {
                    callback?.Invoke(null);
                    return;
                }
            }
            
            // Limit queue size
            if (_pathRequests.Count >= MAX_QUEUED_REQUESTS)
            {
                Debug.LogWarning($"Path request queue full ({MAX_QUEUED_REQUESTS} requests)");
                callback?.Invoke(null);
                return;
            }

            _lastPathRequestTime[entity] = Time.time;
            _pathRequests.Enqueue(new PathRequest 
            { 
                Entity = entity,
                Start = start,
                End = end,
                EntityHeight = entityHeight,
                Callback = callback,
                RequestTime = Time.time
            });
        }

        private void RegisterPath(Entity entity, Path path)
        {
            // Remove old path if exists
            if (_activePaths.TryGetValue(entity, out var oldPath))
            {
                foreach (var chunk in oldPath.ChunksUsed)
                {
                    if (_chunkPathUsers.ContainsKey(chunk))
                    {
                        _chunkPathUsers[chunk].Remove(entity);
                        if (_chunkPathUsers[chunk].Count == 0)
                            _chunkPathUsers.Remove(chunk);
                    }
                }
            }

            // Register new path
            _activePaths[entity] = path;
            foreach (var chunk in path.ChunksUsed)
            {
                if (!_chunkPathUsers.ContainsKey(chunk))
                    _chunkPathUsers[chunk] = new HashSet<Entity>();
                _chunkPathUsers[chunk].Add(entity);
            }
        }

        public void OnChunkModified(int3 chunkPos)
        {
            if (!_chunkPathUsers.TryGetValue(chunkPos, out var entities))
                return;

            foreach (var entity in entities)
            {
                if (_activePaths.TryGetValue(entity, out var path))
                {
                    bool needsInvalidation = false;
                    foreach (var point in path.Points)
                    {
                        var pointChunkPos = ChunkHelper.GetChunkPos(point);
                        if (pointChunkPos.x == chunkPos.x && 
                            pointChunkPos.y == chunkPos.y && 
                            pointChunkPos.z == chunkPos.z)
                        {
                            needsInvalidation = true;
                            break;
                        }
                    }

                    if (needsInvalidation)
                    {
                        path.IsValid = false;
                    }
                }
            }
        }

        private async Task<bool> HasAccessibleNeighbor(int3 pos)
        {
            for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            for (int z = -1; z <= 1; z++)
            {
                if (x == 0 && y == 0 && z == 0) continue;
                var checkPos = pos + new int3(x, y, z);
                if (await CanStandAt(checkPos))
                    return true;
            }
            return false;
        }

        private async Task<Path> FindPath(int3 start, int3 end, float entityHeight)
        {
            // Early check - if target is completely enclosed, don't even try
            if (!await HasAccessibleNeighbor(end))
            {
                return null;
            }

            _openNodes.Clear();
            _closedNodes.Clear();

            var startNode = new PathNode(start);
            startNode.HCost = EstimateDistance(start, end);
            _openNodes.Add(start, startNode);

            int iterations = 0;
            while (_openNodes.Count > 0 && iterations < MAX_PATH_LENGTH)
            {
                iterations++;
                var current = GetLowestFCostNode();

                var dx = math.abs(current.Position.x - end.x);
                var dy = math.abs(current.Position.y - end.y);
                var dz = math.abs(current.Position.z - end.z);
                if (dx <= 1 && dy <= 1 && dz <= 1)
                {
                    var pathPoints = ReconstructPath(current);
                    return new Path(pathPoints);
                }

                _openNodes.Remove(current.Position);
                _closedNodes.Add(current.Position);

                foreach (var neighbor in await GetNeighbors(current.Position, end))
                {
                    if (_closedNodes.Contains(neighbor))
                        continue;

                    float gCost = current.GCost +
                        (IsDiagonal(current.Position, neighbor) ? DIAGONAL_COST : 1f);

                    if (!_openNodes.TryGetValue(neighbor, out var neighborNode))
                    {
                        neighborNode = new PathNode(neighbor);
                        _openNodes.Add(neighbor, neighborNode);
                    }
                    else if (gCost >= neighborNode.GCost)
                        continue;

                    neighborNode.Parent = current;
                    neighborNode.GCost = gCost;
                    neighborNode.HCost = EstimateDistance(neighbor, end);
                }
            }

            return null;
        }

        private float EstimateDistance(int3 a, int3 b)
        {
            return math.abs(a.x - b.x) + math.abs(a.y - b.y) + math.abs(a.z - b.z);
        }

        private bool IsDiagonal(int3 from, int3 to)
        {
            return from.x != to.x && from.z != to.z;
        }

        private PathNode GetLowestFCostNode()
        {
            PathNode lowest = null;
            float lowestFCost = float.MaxValue;

            foreach (var node in _openNodes.Values)
            {
                if (node.FCost < lowestFCost ||
                    (node.FCost == lowestFCost && node.HCost < lowest?.HCost))
                {
                    lowest = node;
                    lowestFCost = node.FCost;
                }
            }

            return lowest;
        }

        private async Task<List<int3>> GetNeighbors(int3 pos, int3 end)
        {
            _neighbors.Clear();
            
            // Check horizontal and vertical neighbors
            var directions = new int3[]
            {
                // Same level
                new int3(1, 0, 0),
                new int3(-1, 0, 0),
                new int3(0, 0, 1),
                new int3(0, 0, -1),
                
                // Climbing up
                new int3(1, 1, 0),
                new int3(-1, 1, 0),
                new int3(0, 1, 1),
                new int3(0, 1, -1),
                
                // Climbing down
                new int3(1, -1, 0),
                new int3(-1, -1, 0),
                new int3(0, -1, 1),
                new int3(0, -1, -1)
            };

            foreach (var dir in directions)
            {
                var checkPos = pos + dir;
                
                // For climbing up
                if (dir.y > 0)
                {
                    // Check if we can stand at the higher position
                    if (await CanStandAt(checkPos))
                    {
                        // Check if the block below is solid (for climbing)
                        if (await _worldAPI.IsBlockSolidAsync(checkPos + new int3(0, -1, 0)))
                        {
                            _neighbors.Add(checkPos);
                        }
                    }
                }
                // For climbing down or same level
                else
                {
                    if (await CanStandAt(checkPos))
                    {
                        _neighbors.Add(checkPos);
                    }
                }
            }

            return _neighbors;
        }

        private async Task<bool> CanStandAt(int3 pos)
        {
            // Position is valid if:
            // 1. Current block is not solid (entity can be here)
            // 2. Block below is solid (entity has support)
            return !await _worldAPI.IsBlockSolidAsync(pos) && 
                   await _worldAPI.IsBlockSolidAsync(pos + new int3(0, -1, 0));
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

        public bool IsPositionValid(int3 pos, float entityHeight)
        {
            return !_worldAPI.IsBlockSolid(pos) && 
                   _worldAPI.IsBlockSolid(pos + new int3(0, -1, 0));
        }

        public async void ProcessPathRequests()
        {
            // Process multiple requests per frame if possible
            int processedThisFrame = 0;
            while (_pathRequests.Count > 0 && 
                   _activePathfinding.Count < MAX_CONCURRENT_PATHFINDS &&
                   processedThisFrame < MAX_CONCURRENT_PATHFINDS)
            {
                var request = _pathRequests.Peek();
                
                // Skip if entity already pathfinding
                if (_activePathfinding.Contains(request.Entity))
                {
                    _pathRequests.Dequeue();
                    continue;
                }

                // Check request age
                if (Time.time - request.RequestTime > 5f)
                {
                    Debug.LogWarning("Discarding stale path request");
                    _pathRequests.Dequeue();
                    continue;
                }

                _pathRequests.Dequeue();
                processedThisFrame++;
                
                _activePathfinding.Add(request.Entity);
                try
                {
                    var path = await FindPath(request.Start, request.End, request.EntityHeight);
                    if (path != null)
                    {
                        RegisterPath(request.Entity, path);
                    }
                    request.Callback?.Invoke(path);
                }
                finally
                {
                    _activePathfinding.Remove(request.Entity);
                }
            }
        }
    }
}