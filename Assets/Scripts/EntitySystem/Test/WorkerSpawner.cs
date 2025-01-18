using UnityEngine;
using EntitySystem.Core;
using EntitySystem.Core.Spawning;
using EntitySystem.Core.World;
using EntitySystem.Entities.Examples;
using JobSystem.Core;
using Unity.Mathematics;
using WorldSystem;
using Random = UnityEngine.Random;

public class WorkerSpawner : EntitySpawner
{
    [Header("Test Settings")]
    [SerializeField] private float jobSpawnInterval = 5f;
    
    private float _nextJobTime;
    private IWorldSystem _worldSystem;
    private bool _canSpawnJobs;

    protected override void OnSystemInitialized()
    {
        _worldSystem = EntityManager.GetWorldSystem();
        
        // Verify job system is available
        if (EntityManager.GetJobSystem() != null)
        {
            _canSpawnJobs = true;
        }
        else
        {
            Debug.LogError($"[{gameObject.name}] No JobSystem available!");
        }
    }

    protected override void OnBeginSpawning()
    {
        if (!_canSpawnJobs)
        {
            Debug.LogError($"[{gameObject.name}] Cannot begin spawning: JobSystem not available");
            return;
        }

        // Spawn initial workers
        for (int i = 0; i < maxEntities; i++)
        {
            SpawnWorker();
        }
    }

    protected override void OnEndSpawning()
    {
        // Clean up if needed
    }

    private void Update()
    {
        if (Time.time >= _nextJobTime)
        {
            _nextJobTime = Time.time + jobSpawnInterval;
        }
    }

    private void SpawnWorker()
    {
        var spawnPosition = GetValidSpawnPosition();
        if (!spawnPosition.HasValue)
        {
            Debug.LogWarning($"[{gameObject.name}] Failed to find valid spawn position");
            return;
        }

        var worker = EntityManager.CreateEntity<WorkerEntity>(spawnPosition.Value);
        if (worker != null)
        {
        }
        else
        {
            Debug.LogError($"[{gameObject.name}] Failed to create worker entity");
        }
    }

    private Vector3? GetValidSpawnPosition()
    {
        PathFinder pathFinder = new PathFinder(_worldSystem);
        pathFinder.EnableDebugMode(true);
        
        for (int attempts = 0; attempts < 10; attempts++)
        {
            // Get random XZ position within spawn radius (using whole numbers)
            Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
            Vector3Int testPos = new Vector3Int(
                Mathf.RoundToInt(transform.position.x + randomCircle.x),
                0,
                Mathf.RoundToInt(transform.position.z + randomCircle.y)
            );
            
            // Get the highest solid block at this XZ coordinate
            int highestBlock = _worldSystem.GetHighestSolidBlock(testPos.x, testPos.z);
            if (highestBlock < 0)
            {
                Debug.LogWarning($"No ground found at ({testPos.x}, {testPos.z})");
                continue;
            }

            // Position should be exactly one block above the ground
            Vector3Int spawnPos = new Vector3Int(testPos.x, highestBlock + 1, testPos.z);
            
            // Double check the position is valid
            if (!_worldSystem.IsBlockSolid(new int3(spawnPos.x, spawnPos.y - 1, spawnPos.z)) ||  // Ground block should be solid
                _worldSystem.IsBlockSolid(new int3(spawnPos.x, spawnPos.y, spawnPos.z)) ||       // Feet position should be clear
                _worldSystem.IsBlockSolid(new int3(spawnPos.x, spawnPos.y + 1, spawnPos.z)))     // Head position should be clear
            {
                Debug.LogWarning($"Invalid spawn position at {spawnPos}");
                continue;
            }

            Debug.Log($"Found valid spawn position at {spawnPos} (ground at Y={highestBlock})");
            return spawnPos;
        }
        
        Debug.LogWarning($"Failed to find valid spawn position after 10 attempts");
        return null;
    }
}