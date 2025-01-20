using UnityEngine;

namespace EntitySystem.Core.Components
{
    public class ItemComponent : EntityComponent
    {
        [SerializeField] private string _itemId;      // Type of item (e.g., "Wood", "Stone")
        [SerializeField] private int _spaceRequired = 1;
        
        // Each item instance gets a unique ID when created
        private string _uniqueInstanceId;

        protected override void OnInitialize(EntityManager entityManager)
        {
            base.OnInitialize(entityManager);
            // Generate a unique ID for this specific item instance
            _uniqueInstanceId = System.Guid.NewGuid().ToString();
        }

        public string ItemId { get => _itemId; set => _itemId = value; }
        public int SpaceRequired { get => _spaceRequired; set => _spaceRequired = value; }
        public string UniqueInstanceId => _uniqueInstanceId;

        // Helper method to identify specific items
        public bool Matches(ItemComponent other)
        {
            return other != null && _uniqueInstanceId == other._uniqueInstanceId;
        }
    }
} 