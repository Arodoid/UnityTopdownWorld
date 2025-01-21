using UnityEngine;
using Unity.Mathematics;
using EntitySystem.Core.Components;
using EntitySystem.Data;

namespace EntitySystem.Core.Jobs
{
    public class HaulItemJob : IJob
    {
        private EntityHandle _itemToPickup;
        private int3 _dropLocation;
        private Entity _worker;
        private MovementComponent _movement;
        private ItemPickupComponent _pickup;
        private InventoryComponent _inventory;
        
        private enum State
        {
            MovingToItem,
            AttemptingPickup,
            MovingToDrop,
            AttemptingDrop,
            Complete,
            Failed
        }
        
        private State _currentState;

        public HaulItemJob(EntityHandle item, int3 dropLocation)
        {
            _itemToPickup = item;
            _dropLocation = dropLocation;
        }

        public bool CanAssignTo(Entity worker)
        {
            return worker.HasComponent<MovementComponent>() && 
                   worker.HasComponent<ItemPickupComponent>() &&
                   worker.HasComponent<InventoryComponent>();
        }

        public void Start(Entity worker)
        {
            _worker = worker;
            _movement = worker.GetComponent<MovementComponent>();
            _pickup = worker.GetComponent<ItemPickupComponent>();
            _inventory = worker.GetComponent<InventoryComponent>();

            // Verify item still exists and get its position
            var itemPos = worker.EntityManager.GetEntityPosition(_itemToPickup);
            if (!itemPos.HasValue)
            {
                Debug.Log("Haul job failed: Item no longer exists");
                _currentState = State.Failed;
                return;
            }

            // Start moving to item
            if (_movement.MoveTo(new int3(
                Mathf.FloorToInt(itemPos.Value.x),
                Mathf.FloorToInt(itemPos.Value.y),
                Mathf.FloorToInt(itemPos.Value.z))))
            {
                _currentState = State.MovingToItem;
                _movement.OnDestinationReached += OnReachedItem;
            }
            else
            {
                Debug.Log("Haul job failed: Cannot path to item");
                _currentState = State.Failed;
            }
        }

        private void OnReachedItem()
        {
            _movement.OnDestinationReached -= OnReachedItem;
            _currentState = State.AttemptingPickup;
        }

        private void OnReachedDropLocation()
        {
            _movement.OnDestinationReached -= OnReachedDropLocation;
            _currentState = State.AttemptingDrop;
        }

        public bool Update()
        {
            switch (_currentState)
            {
                case State.MovingToItem:
                    // Wait for OnReachedItem callback
                    break;

                case State.AttemptingPickup:
                    if (_pickup.TryPickupItem(_itemToPickup))
                    {
                        // Successfully picked up, now move to drop location
                        if (_movement.MoveTo(_dropLocation))
                        {
                            _currentState = State.MovingToDrop;
                            _movement.OnDestinationReached += OnReachedDropLocation;
                        }
                        else
                        {
                            Debug.Log("Haul job failed: Cannot path to drop location");
                            _currentState = State.Failed;
                        }
                    }
                    else
                    {
                        Debug.Log("Haul job failed: Could not pick up item");
                        _currentState = State.Failed;
                    }
                    break;

                case State.MovingToDrop:
                    // Wait for OnReachedDropLocation callback
                    break;

                case State.AttemptingDrop:
                    // Try to drop the first item (should be the one we picked up)
                    if (_inventory.TryDropItem(0))
                    {
                        Debug.Log("Haul job completed successfully");
                        _currentState = State.Complete;
                    }
                    else
                    {
                        Debug.Log("Haul job failed: Could not drop item");
                        _currentState = State.Failed;
                    }
                    break;

                case State.Failed:
                case State.Complete:
                    return true;
            }
            
            return false;
        }

        public void Cancel()
        {
            // Clean up event subscriptions
            _movement.OnDestinationReached -= OnReachedItem;
            _movement.OnDestinationReached -= OnReachedDropLocation;
            
            // Stop moving
            _movement.Stop();
        }
    }
}