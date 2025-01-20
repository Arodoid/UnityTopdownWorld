using UnityEngine;
using Unity.Mathematics;
using System.Linq;
using EntitySystem.Data;

namespace EntitySystem.Core.Components
{
    public class ItemPickupComponent : EntityComponent
    {
        [SerializeField] private float _pickupRadius = 1.5f;
        private InventoryComponent _inventory;
        
        // Target item to pick up (if any)
        private EntityHandle? _targetItemHandle;
        private string _targetInstanceId; // Store the unique ID of the item we want
        private bool _isWaitingToPickup;

        protected override void OnInitialize(EntityManager entityManager)
        {
            _inventory = Entity.GetComponent<InventoryComponent>();
            if (_inventory == null)
            {
                Debug.LogError("ItemPickupComponent requires an InventoryComponent!");
            }
        }

        // Called by job system to set a specific item to pick up
        public bool SetTargetItem(EntityHandle itemHandle)
        {
            var item = Entity.EntityManager.GetComponent<ItemComponent>(itemHandle);
            if (item == null) return false;

            // Check if we have space for this item
            if (!_inventory.CanAddItem(item))
            {
                Debug.Log("Can't pick up item: inventory full");
                return false;
            }

            _targetItemHandle = itemHandle;
            _targetInstanceId = item.UniqueInstanceId; // Store the unique ID
            _isWaitingToPickup = true;
            return true;
        }

        public void ClearTargetItem()
        {
            _targetItemHandle = null;
            _targetInstanceId = null;
            _isWaitingToPickup = false;
        }

        // Called by movement system when entity arrives at destination
        public void OnArrivedAtDestination()
        {
            if (_isWaitingToPickup && _targetItemHandle.HasValue)
            {
                TryPickupTargetItem();
            }
        }

        private bool TryPickupTargetItem()
        {
            if (!_targetItemHandle.HasValue) return false;

            // Verify this is still the exact item we want
            var targetItem = Entity.EntityManager.GetComponent<ItemComponent>(_targetItemHandle.Value);
            if (targetItem == null || targetItem.UniqueInstanceId != _targetInstanceId)
            {
                Debug.Log("Target item no longer exists or has changed");
                ClearTargetItem();
                return false;
            }

            // Check if we're in range
            var itemPosition = Entity.EntityManager.GetEntityPosition(_targetItemHandle.Value);
            if (itemPosition == null)
            {
                Debug.Log("Target item no longer exists");
                ClearTargetItem();
                return false;
            }

            float distance = Vector3.Distance(transform.position, itemPosition.Value);
            if (distance <= _pickupRadius)
            {
                if (_inventory.TryAddItem(_targetItemHandle.Value))
                {
                    Debug.Log($"Successfully picked up {targetItem.ItemId} (Instance: {targetItem.UniqueInstanceId})");
                    Entity.EntityManager.DestroyEntity(_targetItemHandle.Value);
                    ClearTargetItem();
                    return true;
                }
            }
            else
            {
                Debug.Log($"Too far from target item: {distance} units");
            }

            return false;
        }

        // For debugging
        public bool HasTargetItem => _targetItemHandle.HasValue;
        public EntityHandle? CurrentTarget => _targetItemHandle;
    }
} 
