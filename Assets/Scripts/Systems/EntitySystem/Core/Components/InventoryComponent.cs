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
        private List<EntityHandle> _items = new();

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
                _items.Add(itemHandle);
                _currentCapacity += item.SpaceRequired;
                
                // Move the item to this entity's position (effectively "picking it up")
                Entity.EntityManager.SetEntityPosition(itemHandle, 
                    new int3((int)transform.position.x, (int)transform.position.y, (int)transform.position.z));
                
                Debug.Log($"Added {item.ItemId} to inventory. Space used: {_currentCapacity}/{_maxCapacity}");
                return true;
            }
            return false;
        }

        public bool TryDropItem(int index)
        {
            if (index >= 0 && index < _items.Count)
            {
                var itemHandle = _items[index];
                var item = Entity.EntityManager.GetComponent<ItemComponent>(itemHandle);
                if (item != null)
                {
                    _currentCapacity -= item.SpaceRequired;
                    _items.RemoveAt(index);
                    
                    Vector3 dropPosition = transform.position + UnityEngine.Random.insideUnitSphere * 1f;
                    dropPosition.y = transform.position.y;
                    
                    Entity.EntityManager.SetEntityPosition(itemHandle, 
                        new int3((int)dropPosition.x, (int)dropPosition.y, (int)dropPosition.z));
                    
                    Debug.Log($"Dropped {item.ItemId}. Space remaining: {_maxCapacity - _currentCapacity}");
                    return true;
                }
            }
            return false;
        }

        public void LogInventoryContents()
        {
            Debug.Log($"Inventory contents ({_currentCapacity}/{_maxCapacity} space used):");
            foreach (var handle in _items)
            {
                var item = Entity.EntityManager.GetComponent<ItemComponent>(handle);
                if (item != null)
                {
                    Debug.Log($"- {item.ItemId} (space: {item.SpaceRequired})");
                }
            }
        }
    }
}