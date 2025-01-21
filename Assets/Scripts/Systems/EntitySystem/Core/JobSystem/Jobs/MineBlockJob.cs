using UnityEngine;
using Unity.Mathematics;
using EntitySystem.Core.Components;
using WorldSystem.API;
using WorldSystem.Data;

namespace EntitySystem.Core.Jobs
{
    public class MineBlockJob : IJob
    {
        private int3 _blockPosition;
        private Entity _worker;
        private MovementComponent _movement;
        private WorldSystemAPI _worldAPI;
        
        private enum State
        {
            MovingToBlock,
            Mining,
            Complete,
            Failed
        }
        
        private State _currentState;

        public MineBlockJob(int3 blockPosition, WorldSystemAPI worldAPI)
        {
            _blockPosition = blockPosition;
            _worldAPI = worldAPI;
        }

        public bool CanAssignTo(Entity worker)
        {
            return worker.HasComponent<MovementComponent>();
        }

        public void Start(Entity worker)
        {
            _worker = worker;
            _movement = worker.GetComponent<MovementComponent>();

            // Find a valid position next to the block
            var standPosition = FindMiningPosition();
            if (standPosition.HasValue)
            {
                if (_movement.MoveTo(standPosition.Value))
                {
                    _currentState = State.MovingToBlock;
                    _movement.OnDestinationReached += OnReachedMiningPosition;
                }
                else
                {
                    Debug.Log("Mine job failed: Cannot path to block");
                    _currentState = State.Failed;
                }
            }
            else
            {
                Debug.Log("Mine job failed: No valid position to mine from");
                _currentState = State.Failed;
            }
        }

        private int3? FindMiningPosition()
        {
            // Check a larger area around the block (3x3x3 cube centered on block)
            for (int x = -2; x <= 2; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int z = -2; z <= 2; z++)
                    {
                        var checkPos = _blockPosition + new int3(x, y, z);
                        var distanceSquared = x*x + y*y + z*z;
                        
                        // Skip positions too far away (using 3.5 blocks as max reach)
                        if (distanceSquared > 12) continue;

                        // Check if this is a valid standing position
                        if (IsValidMiningPosition(checkPos, _blockPosition))
                        {
                            return checkPos;
                        }
                    }
                }
            }
            return null;
        }

        private bool IsValidMiningPosition(int3 standPos, int3 targetBlock)
        {
            try
            {
                // Must be air to stand in
                if (_worldAPI.GetBlockType(standPos).Result != BlockType.Air)
                    return false;

                // Must have solid ground below
                if (_worldAPI.GetBlockType(standPos + new int3(0, -1, 0)).Result == BlockType.Air)
                    return false;

                // Check if we can "see" the target block (simple line of sight check)
                var direction = targetBlock - standPos;
                float distance = math.length(direction);
                
                // Too far to reach
                if (distance > 3.5f) 
                    return false;

                // Simple line of sight check
                // Could be improved with proper ray casting if needed
                var normalized = math.normalize(direction);
                var checkPos = standPos;
                var step = normalized * 0.5f; // Check every half block

                for (float d = 0; d < distance; d += 0.5f)
                {
                    checkPos = new int3(
                        (int)(standPos.x + step.x * d),
                        (int)(standPos.y + step.y * d),
                        (int)(standPos.z + step.z * d)
                    );

                    // Skip the start and end positions
                    if (checkPos.Equals(standPos) || checkPos.Equals(targetBlock))
                        continue;

                    // If we hit any solid block in between, invalid position
                    if (_worldAPI.GetBlockType(checkPos).Result != BlockType.Air)
                        return false;
                }

                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error checking mining position: {e.Message}");
                return false;
            }
        }

        private void OnReachedMiningPosition()
        {
            _movement.OnDestinationReached -= OnReachedMiningPosition;
            _currentState = State.Mining;
        }

        public bool Update()
        {
            switch (_currentState)
            {
                case State.MovingToBlock:
                    // Wait for OnReachedMiningPosition callback
                    break;

                case State.Mining:
                    // Set block to air
                    _worldAPI.SetBlock(_blockPosition, BlockType.Air).Wait();
                    _currentState = State.Complete;
                    break;

                case State.Failed:
                case State.Complete:
                    return true;
            }
            
            return false;
        }

        public void Cancel()
        {
            _movement.OnDestinationReached -= OnReachedMiningPosition;
            _movement.Stop();
        }
    }
} 