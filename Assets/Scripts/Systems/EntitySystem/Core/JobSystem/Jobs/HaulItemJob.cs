using UnityEngine;
using Unity.Mathematics;
using EntitySystem.Core.Components;
using EntitySystem.Data;
using EntitySystem.Core.Jobs;

namespace EntitySystem.Core.Jobs
{
    public class HaulItemJob : IJob
    {
        private EntityHandle _itemToPickup;
        public EntityHandle ItemToPickup => _itemToPickup;  // Public read-only property
        private int3 _dropLocation;
        private Entity _worker;
        private BlockWorldPhysicsComponent _movement;
        private ItemPickupComponent _pickup;
        private InventoryComponent _inventory;
        
        private State _currentState = State.NotStarted;
        private bool _pickupComplete;
        private bool _dropComplete;
        
        public enum State
        {
            NotStarted,
            MovingToItem,
            AttemptingPickup,
            MovingToDrop,
            AttemptingDrop,
            Complete,
            Failed
        }

        public bool IsComplete => _currentState == State.Complete || _currentState == State.Failed;
        public static float JobTypePriority => 0.8f; // Lower than mining priority

        public HaulItemJob(EntityHandle itemToPickup, int3 dropLocation)
        {
            _itemToPickup = itemToPickup;
            _dropLocation = dropLocation;
            _currentState = State.NotStarted;
        }

        public void Start(Entity worker)
        {
            _worker = worker;
            _movement = worker.GetComponent<BlockWorldPhysicsComponent>();
            _pickup = worker.GetComponent<ItemPickupComponent>();
            _inventory = worker.GetComponent<InventoryComponent>();

            int3 itemPos;
            if (!worker.EntityManager.TryGetEntityPosition(_itemToPickup, out itemPos))
            {
                Debug.Log("Haul job failed: Item no longer exists");
                _currentState = State.Failed;
                return;
            }

            _movement.MoveTo(itemPos, 1, OnMovementComplete);
            _currentState = State.MovingToItem;
        }

        private void OnMovementComplete(BlockWorldPhysicsComponent.MovementResult result)
        {
            switch (result)
            {
                case BlockWorldPhysicsComponent.MovementResult.Reached:
                    if (_currentState == State.MovingToItem)
                    {
                        _currentState = State.AttemptingPickup;
                    }
                    else if (_currentState == State.MovingToDrop)
                    {
                        _currentState = State.AttemptingDrop;
                    }
                    break;
                
                case BlockWorldPhysicsComponent.MovementResult.Unreachable:
                case BlockWorldPhysicsComponent.MovementResult.Failed:
                case BlockWorldPhysicsComponent.MovementResult.Interrupted:
                    Debug.Log($"Movement failed: {result}");
                    _currentState = State.Failed;
                    break;
            }
        }

        public bool Update()
        {
            switch (_currentState)
            {
                case State.MovingToItem:
                case State.MovingToDrop:
                    return false; // Wait for movement callback

                case State.AttemptingPickup:
                    if (_pickup.TryPickupItem(_itemToPickup))
                    {
                        _movement.MoveTo(_dropLocation, 1, OnMovementComplete);
                        _currentState = State.MovingToDrop;
                    }
                    else
                    {
                        Debug.Log("Haul job failed: Could not pick up item");
                        _currentState = State.Failed;
                    }
                    return false;

                case State.AttemptingDrop:
                    Debug.Log($"Attempting to drop at position: {_dropLocation}");
                    if (_inventory.TryDropItem(0, _dropLocation))
                    {
                        Debug.Log($"Successfully dropped at: {_dropLocation}");
                        _currentState = State.Complete;
                    }
                    else
                    {
                        Debug.Log("Haul job failed: Could not drop item");
                        _currentState = State.Failed;
                    }
                    return true;

                case State.Failed:
                case State.Complete:
                    return true;
                
                default:
                    return false;
            }
        }

        public float GetPriority(Entity worker)
        {
            if (!CanAssignTo(worker))
                return float.MinValue;

            int3 workerPos;
            if (!worker.EntityManager.TryGetEntityPosition(worker.Id, out workerPos))
                return float.MinValue;

            int3 itemPos;
            if (!worker.EntityManager.TryGetEntityPosition(_itemToPickup, out itemPos))
                return float.MinValue;

            // Manhattan distance in block space
            float distance = math.abs(workerPos.x - itemPos.x) + 
                            math.abs(workerPos.y - itemPos.y) + 
                            math.abs(workerPos.z - itemPos.z);
            return JobTypePriority - (distance / 1000f);
        }

        public void Cancel()
        {
            _movement?.Stop();
            _currentState = State.Failed;
        }

        public bool CanAssignTo(Entity worker)
        {
            if (_currentState != State.NotStarted)
                return false;
            
            // Check if worker has required components
            return worker != null && 
                   worker.HasComponent<BlockWorldPhysicsComponent>() &&
                   worker.HasComponent<ItemPickupComponent>() &&
                   worker.HasComponent<InventoryComponent>();
        }

        // Rest of implementation...
    }
}