using UnityEngine;
using VoxelGame.Entities;
using UnityEngine.UI; // If using UI button

public class NPCSpawner : MonoBehaviour
{
    [SerializeField] private Button spawnButton; // Optional: for GUI button
    
    private void Start()
    {
        // Optional: Auto-spawn on start
        SpawnNPCAtRandomPosition();
        
        // Optional: Hook up UI button if assigned
        if (spawnButton != null)
        {
            spawnButton.onClick.AddListener(SpawnNPCAtRandomPosition);
        }
    }

    public void SpawnNPCAtRandomPosition()
    {
        // Get a random position near world center
        Vector3Int spawnPos = new Vector3Int(
            Random.Range(-10, 10),
            100, // Adjust this based on your terrain height
            Random.Range(-10, 10)
        );

        // Create the NPC entity
        var npcData = EntityDataManager.Instance.CreateEntity(
            EntityType.NPC,
            spawnPos,
            Quaternion.identity
        );

        Debug.Log($"Spawned NPC at {spawnPos}");
    }
} 