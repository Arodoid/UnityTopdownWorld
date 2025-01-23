using UnityEngine;
using Unity.Mathematics;

namespace EntitySystem.Core.Components
{
    public class ChestComponent : EntityComponent
    {
        [SerializeField] private int _maxStackSize = 10;
        private int3 _storagePosition;

        protected override void OnInitialize(EntityManager entityManager)
        {
            base.OnInitialize(entityManager);
            
            if (!entityManager.TryGetEntityPosition(Entity.Id, out _storagePosition))
            {
                Debug.LogError("Failed to get storage position for chest");
                return;
            }
        }

        public bool HasSpace()
        {
            // Count how many items are at this position
            int itemCount = 0;
            foreach (var entity in Entity.EntityManager.GetAllEntities())
            {
                if (entity.GetComponent<ItemComponent>() == null) continue;
                
                int3 itemPos;
                if (Entity.EntityManager.TryGetEntityPosition(entity.Id, out itemPos) 
                    && itemPos.Equals(_storagePosition))
                {
                    itemCount++;
                }
            }
            
            return itemCount < _maxStackSize;
        }

        public int3? GetEmptySlot()
        {
            if (HasSpace())
            {
                Debug.Log($"Chest returning storage position: {_storagePosition}");
                return _storagePosition;
            }
            return null;
        }

        public bool IsValidStoragePosition(int3 position)
        {
            return position.Equals(_storagePosition);
        }

        // Debug visualization
        private void OnDrawGizmosSelected()
        {
            if (Application.isPlaying)
            {
                Gizmos.color = HasSpace() ? Color.green : Color.red;
                Gizmos.DrawWireCube(transform.position, Vector3.one);
                
                // Show capacity
                UnityEditor.Handles.Label(transform.position + Vector3.up, 
                    $"Max Stack: {_maxStackSize}");
            }
        }
    }
} 