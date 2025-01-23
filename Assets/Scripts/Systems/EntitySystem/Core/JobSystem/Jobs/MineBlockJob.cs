using UnityEngine;
using Unity.Mathematics;
using EntitySystem.Core.Components;
using WorldSystem.API;
using WorldSystem.Data;
using System.Threading.Tasks;
using System.Diagnostics;

namespace EntitySystem.Core.Jobs
{
    public class MineBlockJob : IJob
    {
        private int3 _blockPosition;
        private Entity _worker;
        private BlockWorldPhysicsComponent _movement;
        private WorldSystemAPI _worldAPI;
        private bool _miningComplete;
        
        public enum State
        {
            NotStarted,
            Moving,
            Mining,
            Complete,
            Failed
        }
        
        private State _currentState = State.NotStarted;
        public State CurrentState => _currentState;
        public Entity Worker => _worker;

        private readonly Stopwatch _miningTimer = new();
        private const float MINING_TIMEOUT_SECONDS = 30f;
        public static float JobTypePriority => 1.0f;

        // Implement the IsComplete property
        public bool IsComplete => _currentState == State.Complete;

        public MineBlockJob(int3 blockPosition, WorldSystemAPI worldAPI)
        {
            _blockPosition = blockPosition;
            _worldAPI = worldAPI;
        }

        public void Start(Entity worker)
        {
            _worker = worker;
            _movement = worker.GetComponent<BlockWorldPhysicsComponent>();
            
            if (_movement == null)
            {
                UnityEngine.Debug.LogError("No BlockWorldPhysicsComponent found!");
                _currentState = State.Failed;
                return;
            }

            _movement.MoveTo(_blockPosition, 1, OnMovementComplete);
            _currentState = State.Moving;
        }

        private void OnMovementComplete(BlockWorldPhysicsComponent.MovementResult result)
        {
            switch (result)
            {
                case BlockWorldPhysicsComponent.MovementResult.Reached:
                    _currentState = State.Mining;
                    break;
                
                case BlockWorldPhysicsComponent.MovementResult.Unreachable:
                case BlockWorldPhysicsComponent.MovementResult.Failed:
                    UnityEngine.Debug.Log($"Failed to reach mining position: {result}");
                    _currentState = State.Failed;
                    break;
                
                case BlockWorldPhysicsComponent.MovementResult.Interrupted:
                    _currentState = State.Failed;
                    break;
            }
        }

        public bool Update()
        {
            if (_currentState == State.Mining && 
                _miningTimer.Elapsed.TotalSeconds > MINING_TIMEOUT_SECONDS)
            {
                UnityEngine.Debug.LogWarning($"Mining operation timed out");
                _currentState = State.Failed;
                return true;
            }

            switch (_currentState)
            {
                case State.Moving:
                    return false;

                case State.Mining:
                    if (!_miningComplete)
                    {
                        #pragma warning disable CS4014
                        HandleMiningAsync();
                        #pragma warning restore CS4014
                        return false;
                    }
                    return true;

                case State.Complete:
                case State.Failed:
                    return true;
                    
                default:
                    return false;
            }
        }

        private async Task HandleMiningAsync()
        {
            try
            {
                _miningTimer.Start();
                
                if (await _worldAPI.IsBlockSolidAsync(_blockPosition))
                {
                    await _worldAPI.SetBlock(_blockPosition, BlockType.Air);
                    _miningComplete = true;
                    _currentState = State.Complete;
                }
                else
                {
                    _currentState = State.Failed;
                }
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"Error in mining operation: {e}");
                _currentState = State.Failed;
            }
            finally
            {
                _miningTimer.Stop();
            }
        }

        public void Cancel()
        {
            _movement?.Stop();
        }

        public bool CanAssignTo(Entity worker)
        {
            if (_currentState != State.NotStarted)
                return false;
            
            return worker != null && 
                   worker.HasComponent<BlockWorldPhysicsComponent>();
        }

        public float GetPriority(Entity worker)
        {
            if (!CanAssignTo(worker))
                return float.MinValue;

            int3 workerPos;
            if (!worker.EntityManager.TryGetEntityPosition(worker.Id, out workerPos))
                return float.MinValue;

            // Manhattan distance in block space
            float distance = math.abs(workerPos.x - _blockPosition.x) + 
                            math.abs(workerPos.y - _blockPosition.y) + 
                            math.abs(workerPos.z - _blockPosition.z);
            return JobTypePriority - (distance / 1000f);
        }

        public override bool Equals(object obj)
        {
            return obj is MineBlockJob other && 
                   _blockPosition.Equals(other._blockPosition);
        }

        public override int GetHashCode()
        {
            return _blockPosition.GetHashCode();
        }
    }
}