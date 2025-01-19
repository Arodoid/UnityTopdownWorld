using UnityEngine;
using Unity.Mathematics;
using WorldSystem.API;

namespace UISystem.Core.Interactions
{
    public class WorldCoordinateMapper
    {
        private readonly Camera _camera;
        private readonly WorldSystemAPI _worldAPI;
        private readonly float _maxDistance = 100f;
        
        public WorldCoordinateMapper(Camera camera, WorldSystemAPI worldAPI)
        {
            _camera = camera;
            _worldAPI = worldAPI;
        }

        public bool TryGetHoveredBlockPosition(Vector2 screenPosition, out int3 blockPosition)
        {
            blockPosition = default;
            
            // Convert screen point to ray
            Ray ray = _camera.ScreenPointToRay(screenPosition);
            
            // We'll step along the ray checking blocks
            Vector3 currentPos = ray.origin;
            Vector3 step = ray.direction * 0.1f; // Small steps for accuracy
            
            float distance = 0f;
            while (distance < _maxDistance)
            {
                // Convert to block coordinates
                int3 currentBlock = new int3(
                    Mathf.FloorToInt(currentPos.x),
                    Mathf.FloorToInt(currentPos.y),
                    Mathf.FloorToInt(currentPos.z)
                );

                // Check if we've hit a block
                var blockType = _worldAPI.GetBlockType(currentBlock).Result;
                if (blockType != WorldSystem.Data.BlockType.Air)
                {
                    blockPosition = currentBlock;
                    return true;
                }

                // Move along ray
                currentPos += step;
                distance += step.magnitude;
            }

            return false;
        }

        public bool TryGetAdjacentBlockPosition(Vector2 screenPosition, out int3 blockPosition, out int3 existingBlockPosition)
        {
            blockPosition = default;
            existingBlockPosition = default;
            
            Ray ray = _camera.ScreenPointToRay(screenPosition);
            Vector3 currentPos = ray.origin;
            Vector3 step = ray.direction * 0.1f;
            
            float distance = 0f;
            int3 lastAirBlock = default;
            
            while (distance < _maxDistance)
            {
                int3 currentBlock = new int3(
                    Mathf.FloorToInt(currentPos.x),
                    Mathf.FloorToInt(currentPos.y),
                    Mathf.FloorToInt(currentPos.z)
                );

                var blockType = _worldAPI.GetBlockType(currentBlock).Result;
                if (blockType != WorldSystem.Data.BlockType.Air)
                {
                    blockPosition = lastAirBlock;
                    existingBlockPosition = currentBlock;
                    return true;
                }

                lastAirBlock = currentBlock;
                currentPos += step;
                distance += step.magnitude;
            }

            return false;
        }
    }
} 