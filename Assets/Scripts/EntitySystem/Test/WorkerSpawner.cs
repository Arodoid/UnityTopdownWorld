using UnityEngine;
using EntitySystem.Core;
using EntitySystem.Core.Spawning;
using EntitySystem.Core.World;
using EntitySystem.Entities.Examples;
using EntitySystem.Core.Jobs;
using Unity.Mathematics;
using Random = UnityEngine.Random;

public class WorkerSpawner : EntitySpawner
{
    [Header("Test Settings")]
    [SerializeField] private bool spawnTestJobs = true;
    [SerializeField] private float jobSpawnInterval = 5f;
    
    private float _nextJobTime;
    private IWorldAccess _worldAccess;
    private bool _canSpawnJobs;

    protected override void OnSystemInitialized()
    {
        _worldAccess = EntityManager.GetWorldAccess();
        
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
        if (!IsInitialized || !_canSpawnJobs || !spawnTestJobs) return;

        if (Time.time >= _nextJobTime)
        {
            // SpawnTestJob();
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
        for (int attempts = 0; attempts < 10; attempts++)
        {
            Vector3 testPos = transform.position + Random.insideUnitSphere * spawnRadius;
            testPos.y = 100; // Start high

            if (_worldAccess != null)
            {
                int3 blockPos = new int3(
                    Mathf.FloorToInt(testPos.x),
                    Mathf.FloorToInt(testPos.y),
                    Mathf.FloorToInt(testPos.z)
                );

                int groundY = _worldAccess.GetHighestSolidBlock(blockPos.x, blockPos.z);
                if (groundY >= 0)
                {
                    return new Vector3(testPos.x, groundY + 1, testPos.z);
                }
            }
            else
            {
                testPos.y = 0;
                return testPos;
            }
        }
        
        return null;
    }

    // private void SpawnTestJob()
    // {
    //     // Spawn multiple jobs concentrated around a point high in the air
    //     Vector3 jobCenter = new Vector3(0, -50, 0);
    //     float jobSpreadRadius = 10f; // Smaller radius for more concentration

    //     for (int i = 0; i < 3; i++)
    //     {
    //         // Create random position around jobCenter
    //         var randomOffset = Random.insideUnitSphere * jobSpreadRadius;
    //         var jobPosition = jobCenter + randomOffset;
            
    //         var chopJob = new ChopTreeJob(jobPosition);
    //         EntityManager.GetJobSystem().AddGlobalJob(chopJob);
            
    //     }
    // }
}