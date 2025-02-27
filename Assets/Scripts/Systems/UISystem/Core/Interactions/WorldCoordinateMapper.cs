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