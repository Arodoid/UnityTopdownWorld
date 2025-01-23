using UnityEngine;
using Unity.Mathematics;
using System.Linq;
using EntitySystem.Data;

namespace EntitySystem.Core.Components
{
    public class ItemPickupComponent : EntityComponent
    {
        [SerializeField] private int _pickupRadius = 1;
        private InventoryComponent _inventory;
        private EntityHandle? _targetItemHandle;

        protected override void OnInitialize(EntityManager entityManager)
        {
            _inventory = Entity.GetComponent<InventoryComponent>();
            if (_inventory == null)
            {
                Debug.LogError("ItemPickupComponent requires an InventoryComponent!");
            }
        }

        // Simple method to attempt pickup of a specific item
        public bool TryPickupItem(EntityHandle itemHandle)
        {
            int3 itemPosition;
            if (!Entity.EntityManager.TryGetEntityPosition(itemHandle, out itemPosition))
            {
                Debug.LogWarning($"Failed to get item position for handle {itemHandle.Id}");
                return false;
            }

            int3 currentPos;
            if (!Entity.EntityManager.TryGetEntityPosition(Entity.Id, out currentPos))
            {
                Debug.LogWarning($"Failed to get entity position for {Entity.Id}");
                return false;
            }

            // Use grid/Manhattan distance like movement component
            var dx = math.abs(currentPos.x - itemPosition.x);
            var dy = math.abs(currentPos.y - itemPosition.y);
            var dz = math.abs(currentPos.z - itemPosition.z);
            
            if (dx <= _pickupRadius && dy <= _pickupRadius && dz <= _pickupRadius)
            {
                if (!_inventory.CanAddItem(Entity.EntityManager.GetComponent<ItemComponent>(itemHandle)))
                {
                    Debug.LogWarning("Inventory full or can't add item");
                    return false;
                }
                return _inventory.TryAddItem(itemHandle);
            }
            
            Debug.LogWarning($"Too far to pickup: dx={dx}, dy={dy}, dz={dz} > {_pickupRadius}");
            return false;
        }

        public int PickupRadius => _pickupRadius;
    }
} 
