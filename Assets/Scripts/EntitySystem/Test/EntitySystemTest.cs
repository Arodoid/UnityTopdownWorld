using UnityEngine;
using System.Collections;
using EntitySystem.Core;
using EntitySystem.Core.World;
using WorldSystem.Core;
using WorldSystem.Base;
using Unity.Mathematics;

public class EntitySystemTest : MonoBehaviour
{
    [SerializeField] private EntityManager entityManager;
    [SerializeField] private ChunkManager chunkManager;
    [SerializeField] private float spawnDelay = 2f;
    
    [Header("Test Configuration")]
    [SerializeField] private bool spawnDebugEntity = true;
    [SerializeField] private bool spawnCharacters = true;
    [SerializeField] private int characterCount = 5;
    [SerializeField] private float spawnRadius = 10f;
    [SerializeField] private float heightVariation = 5f;
    
    void Start()
    {
        
        if (entityManager == null)
            entityManager = GetComponent<EntityManager>();
            
        IWorldAccess worldAccess = new BlockWorldAccess(chunkManager);
        entityManager.Initialize(worldAccess);
        
        StartCoroutine(SpawnEntitiesAfterDelay());
    }
    
    private IEnumerator SpawnEntitiesAfterDelay()
    {
        yield return new WaitForSeconds(spawnDelay);
        
        // Make sure chunks are loaded before spawning
        yield return new WaitUntil(() => !ReferenceEquals(chunkManager.GetChunk(new int2(0, 0)), null));
        
        // Find highest ground level at spawn point
        int groundY = FindHighestGround(Vector3.zero);
        float baseHeight = groundY + 1.1f; // Just slightly above ground
        
        Debug.Log($"Spawning entities at base height {baseHeight} (ground at {groundY})");
        
        if (spawnDebugEntity)
        {
            SpawnDebugEntity(new Vector3(0, baseHeight, 0));
        }
        
        if (spawnCharacters)
        {
            StartCoroutine(SpawnMobileEntitiesSequentially(baseHeight));
        }
    }
    
    private void SpawnDebugEntity(Vector3 position)
    {
        var entity = entityManager.CreateEntity<TestCharacterEntity>(position);
        entityManager.RegisterForTicks(entity);
    }
    
    private IEnumerator SpawnMobileEntitiesSequentially(float baseHeight)
    {
        for (int i = 0; i < characterCount; i++)
        {
            // Calculate spawn position in a circle
            float angle = (360f / characterCount) * i;
            float x = Mathf.Cos(angle * Mathf.Deg2Rad) * spawnRadius;
            float z = Mathf.Sin(angle * Mathf.Deg2Rad) * spawnRadius;
            
            // Find ground height at this specific x,z position
            Vector3 checkPos = new Vector3(x, 0, z);
            int localGroundY = FindHighestGround(checkPos);
            float y = localGroundY + 1.1f; // Spawn just above ground at this position
            
            Vector3 spawnPos = new Vector3(x, y, z);
            
            Debug.Log($"Spawning entity at {spawnPos} (ground height: {localGroundY})");
            
            var entity = entityManager.CreateEntity<TestCharacterEntity>(spawnPos);
            entityManager.RegisterForTicks(entity);
            
            yield return new WaitForSeconds(0.5f);
        }
    }
    
    private int FindHighestGround(Vector3 position)
    {
        int x = Mathf.FloorToInt(position.x);
        int z = Mathf.FloorToInt(position.z);
        int y = entityManager.GetWorldAccess().GetHighestSolidBlock(x, z);
        Debug.Log($"Found ground at ({x}, {y}, {z}) for position {position}");
        return y;
    }
    
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        
        // Draw spawn radius
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 
            FindHighestGround(Vector3.zero), spawnRadius);
    }
} 