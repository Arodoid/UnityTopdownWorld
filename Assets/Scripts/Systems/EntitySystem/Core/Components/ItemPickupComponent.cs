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
            var itemPosition = Entity.EntityManager.GetEntityPosition(itemHandle);
            if (itemPosition == null) return false;

            float distance = Vector3.Distance(transform.position, itemPosition.Value);
            if (distance <= _pickupRadius)
            {
                return _inventory.TryAddItem(itemHandle);
            }
            
            return false;
        }

        public float PickupRadius => _pickupRadius;
    }
} 
