using UnityEngine;
using Unity.Mathematics;
using WorldSystem.API;

namespace UISystem.Core.Interactions
{
    public class WorldCoordinateMapper
    {
        private readonly Camera _camera;
        private readonly WorldSystemAPI _worldAPI;
        
        public WorldCoordinateMapper(Camera camera, WorldSystemAPI worldAPI)
        {
            _camera = camera;
            _worldAPI = worldAPI;
        }

        public bool TryGetHoveredBlockPosition(Vector2 screenPosition, out int3 blockPosition)
        {
            blockPosition = default;
            
            // For top-down view, we can use a single plane at y=0
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            Ray ray = _camera.ScreenPointToRay(screenPosition);
            
            if (groundPlane.Raycast(ray, out float distance))
            {
                Vector3 worldPoint = ray.GetPoint(distance);
                int x = Mathf.FloorToInt(worldPoint.x);
                int z = Mathf.FloorToInt(worldPoint.z);

                // Start from current view level and work down
                int maxY = _worldAPI.GetCurrentViewLevel();
                
                for (int y = maxY; y >= 0; y--)
                {
                    int3 potentialBlock = new int3(x, y, z);
                    
                    var blockType = _worldAPI.GetBlockType(potentialBlock).Result;
                    if (blockType != WorldSystem.Data.BlockType.Air)
                    {
                        blockPosition = potentialBlock;
                        return true;
                    }
                }
            }

            return false;
        }

        public bool TryGetAdjacentBlockPosition(Vector2 screenPosition, out int3 blockPosition, out int3 existingBlockPosition)
        {
            blockPosition = default;
            existingBlockPosition = default;

            if (TryGetHoveredBlockPosition(screenPosition, out existingBlockPosition))
            {
                // For top-down view, we might want to prioritize the blocks at the same Y level
                int3[] adjacentOffsets = new int3[]
                {
                    new int3(1, 0, 0),   // Right
                    new int3(-1, 0, 0),  // Left
                    new int3(0, 0, 1),   // Forward
                    new int3(0, 0, -1),  // Back
                    new int3(0, 1, 0),   // Above
                    new int3(0, -1, 0)   // Below
                };

                Ray ray = _camera.ScreenPointToRay(screenPosition);
                float bestDistance = float.MaxValue;
                int3? bestPosition = null;

                foreach (var offset in adjacentOffsets)
                {
                    int3 adjacent = existingBlockPosition + offset;
                    Vector3 adjacentCenter = new Vector3(adjacent.x + 0.5f, adjacent.y + 0.5f, adjacent.z + 0.5f);
                    
                    Vector3 toAdjacent = adjacentCenter - ray.origin;
                    float dot = Vector3.Dot(toAdjacent, ray.direction);
                    Vector3 projection = ray.origin + ray.direction * dot;
                    
                    float distance = Vector3.Distance(adjacentCenter, projection);
                    
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestPosition = adjacent;
                    }
                }

                if (bestPosition.HasValue)
                {
                    blockPosition = bestPosition.Value;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetHoveredPosition(Vector2 screenPosition, out int3 position, bool requireExistingBlock = true)
        {
            position = default;
            
            // For top-down view, we can use a single plane at y=0
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            Ray ray = _camera.ScreenPointToRay(screenPosition);
            
            if (groundPlane.Raycast(ray, out float distance))
            {
                Vector3 worldPoint = ray.GetPoint(distance);
                int x = Mathf.FloorToInt(worldPoint.x);
                int z = Mathf.FloorToInt(worldPoint.z);

                // Start from current view level and work down
                int maxY = _worldAPI.GetCurrentViewLevel();
                
                if (requireExistingBlock)
                {
                    // Find first non-air block
                    for (int y = maxY; y >= 0; y--)
                    {
                        int3 potentialBlock = new int3(x, y, z);
                        var blockType = _worldAPI.GetBlockType(potentialBlock).Result;
                        if (blockType != WorldSystem.Data.BlockType.Air)
                        {
                            position = potentialBlock;
                            return true;
                        }
                    }
                    return false;
                }
                else
                {
                    // Just return the position at current view level
                    position = new int3(x, maxY, z);
                    return true;
                }
            }

            return false;
        }

        // Helper method to get box bounds from two points
        public (int3 min, int3 max) GetBoxBounds(int3 start, int3 end)
        {
            return (
                new int3(
                    Mathf.Min(start.x, end.x),
                    Mathf.Min(start.y, end.y),
                    Mathf.Min(start.z, end.z)
                ),
                new int3(
                    Mathf.Max(start.x, end.x),
                    Mathf.Max(start.y, end.y),
                    Mathf.Max(start.z, end.z)
                )
            );
        }
    }
} 