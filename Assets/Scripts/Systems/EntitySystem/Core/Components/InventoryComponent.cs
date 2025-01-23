using UnityEngine;
using System.Collections.Generic;
using EntitySystem.Data;
using Unity.Mathematics;

namespace EntitySystem.Core.Components
{
    public class InventoryComponent : EntityComponent
    {
        [SerializeField] private int _maxCapacity = 10;
        private int _currentCapacity = 0;
        
        // Store item data instead of handles
        private class StoredItem
        {
            public string ItemId;
            public int SpaceRequired;
            // Add any other properties we need to recreate the item
        }
        
        private List<StoredItem> _items = new();

        public bool HasEmptySlot()
        {
            return _currentCapacity < _maxCapacity;
        }

        public bool CanAddItem(ItemComponent item)
        {
            return _currentCapacity + item.SpaceRequired <= _maxCapacity;
        }

        public bool TryAddItem(EntityHandle itemHandle)
        {
            var item = Entity.EntityManager.GetComponent<ItemComponent>(itemHandle);
            if (item == null) return false;

            if (CanAddItem(item))
            {
                // Store the item's data
                var storedItem = new StoredItem
                {
                    ItemId = item.ItemId,
                    SpaceRequired = item.SpaceRequired
                };
                
                _items.Add(storedItem);
                _currentCapacity += item.SpaceRequired;
                
                // Destroy the world entity
                Entity.EntityManager.DestroyEntity(itemHandle);
                
                return true;
            }
            return false;
        }

        public bool TryDropItem(int index, int3 dropPosition)
        {
            if (index >= 0 && index < _items.Count)
            {
                var storedItem = _items[index];
                
                // Create new entity at drop location
                var newItemHandle = Entity.EntityManager.CreateEntity(storedItem.ItemId, dropPosition);
                if (newItemHandle != EntityHandle.Invalid)
                {
                    _currentCapacity -= storedItem.SpaceRequired;
                    _items.RemoveAt(index);
                    
                    return true;
                }
            }
            return false;
        }
    }
}