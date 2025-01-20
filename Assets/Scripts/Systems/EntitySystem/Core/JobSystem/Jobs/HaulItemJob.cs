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
        
        private enum State
        {
            MovingToItem,
            PickingUp,
            MovingToDrop,
            Dropping,
            Complete
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
                   worker.HasComponent<ItemPickupComponent>();
        }

        public void Start(Entity worker)
        {
            _worker = worker;
            _movement = worker.GetComponent<MovementComponent>();
            _pickup = worker.GetComponent<ItemPickupComponent>();
            
            // Start moving to item
            var itemPos = worker.EntityManager.GetEntityPosition(_itemToPickup);
            if (itemPos.HasValue)
            {
                if (_movement.MoveTo(new int3(
                    Mathf.FloorToInt(itemPos.Value.x),
                    Mathf.FloorToInt(itemPos.Value.y),
                    Mathf.FloorToInt(itemPos.Value.z))))
                {
                    _currentState = State.MovingToItem;
                }
                else
                {
                    _currentState = State.Complete;
                }
            }
            else
            {
                _currentState = State.Complete;
            }
        }

        public bool Update()
        {
            switch (_currentState)
            {
                case State.MovingToItem:
                    if (!_movement.IsMoving)
                    {
                        _pickup.SetTargetItem(_itemToPickup);
                        _currentState = State.PickingUp;
                    }
                    break;

                case State.PickingUp:
                    if (!_pickup.HasTargetItem)
                    {
                        if (_movement.MoveTo(_dropLocation))
                        {
                            _currentState = State.MovingToDrop;
                        }
                        else
                        {
                            _currentState = State.Complete;
                        }
                    }
                    break;

                case State.MovingToDrop:
                    if (!_movement.IsMoving)
                    {
                        // Drop item logic here
                        _currentState = State.Complete;
                    }
                    break;

                case State.Complete:
                    return true;
            }
            
            return false;
        }

        public void Cancel()
        {
            _movement?.Stop();
            _pickup?.ClearTargetItem();
        }
    }
} 