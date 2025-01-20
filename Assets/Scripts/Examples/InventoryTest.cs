using UnityEngine;
using Unity.Mathematics;
using EntitySystem.API;
using EntitySystem.Data;      // For EntityHandle
using EntitySystem.Core;      // For EntityManager
using EntitySystem.Core.Components;

public class InventoryTest : MonoBehaviour
{
    [SerializeField] private EntityManager _entityManager;
    private EntitySystemAPI _entityAPI;
    private EntityHandle _colonist;

    void Start()
    {
        _entityAPI = new EntitySystemAPI(_entityManager);
        SpawnTestEntities();
    }

    void SpawnTestEntities()
    {
        // Spawn colonist
        _colonist = _entityAPI.CreateEntity("Colonist", new int3(5, 0, 5));

        // Spawn some items around the colonist
        SpawnItem("WoodItem", new int3(6, 0, 5));
        SpawnItem("StoneItem", new int3(5, 0, 6));
        SpawnItem("WoodItem", new int3(4, 0, 5));
    }

    void SpawnItem(string itemType, int3 position)
    {
        _entityAPI.CreateEntity(itemType, position);
    }

    void Update()
    {
        // Press I to view inventory
        if (Input.GetKeyDown(KeyCode.I))
        {
            var inventory = _entityManager.GetComponent<InventoryComponent>(_colonist);
            inventory?.LogInventoryContents();
        }

        // Press Space to drop first item in inventory
        if (Input.GetKeyDown(KeyCode.Space))
        {
            var inventory = _entityManager.GetComponent<InventoryComponent>(_colonist);
            inventory?.TryDropItem(0);  // Drop item from first slot
        }
    }
} 