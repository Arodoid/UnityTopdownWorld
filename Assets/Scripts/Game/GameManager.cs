using UnityEngine;
using System.Collections;
using System.Threading.Tasks;
using WorldSystem;
using EntitySystem.Core;
using EntitySystem.Core.World;
using EntitySystem.Core.Spawning;
using WorldSystem.Base;

public class GameManager : MonoBehaviour
{
    [Header("System References")]
    [SerializeField] private EntityManager entityManager;
    [SerializeField] private ChunkManager chunkManager;
    [SerializeField] private EntitySpawner[] entitySpawners;
    
    [Header("World Settings")]
    [SerializeField] private string worldName = "TestWorld";
    [SerializeField] private bool createNewWorld = true;

    private BlockWorldAccess _worldAccess;
    private bool _isInitialized;

    public bool IsInitialized => _isInitialized;
    public BlockWorldAccess WorldAccess => _worldAccess ?? 
        (_worldAccess = new BlockWorldAccess(chunkManager));

    private void Start()
    {
        InitializeWorld();
        
        StartCoroutine(InitializeSystems());
    }

    private IEnumerator InitializeSystems()
    {
        yield return WaitForChunkManager();
        
        _worldAccess = new BlockWorldAccess(chunkManager);
        
        if (!InitializeEntitySystem())
        {
            Debug.LogError("Failed to initialize entity system!");
            yield break;
        }
        
        if (!InitializeSpawners())
        {
            Debug.LogError("Failed to initialize spawners!");
            yield break;
        }
        
        _isInitialized = true;
        
        EnableSpawners();
        
        Debug.Log("Game systems initialized successfully!");
    }

    private IEnumerator WaitForChunkManager()
    {
        if (chunkManager == null)
        {
            Debug.LogError("ChunkManager reference is missing!");
            yield break;
        }

        while (!chunkManager.IsInitialLoadComplete())
        {
            yield return null;
        }
    }

    private bool InitializeEntitySystem()
    {
        if (entityManager == null)
        {
            Debug.LogError("EntityManager reference is missing!");
            return false;
        }

        entityManager.Initialize(_worldAccess);
        return true;
    }

    private bool InitializeSpawners()
    {
        if (entitySpawners == null || entitySpawners.Length == 0)
        {
            Debug.LogWarning("No entity spawners assigned!");
            return false;
        }

        foreach (var spawner in entitySpawners)
        {
            if (spawner != null)
            {
                spawner.gameObject.SetActive(false);
                spawner.Initialize(entityManager);
            }
        }
        return true;
    }

    private void EnableSpawners()
    {
        foreach (var spawner in entitySpawners)
        {
            if (spawner != null)
            {
                spawner.gameObject.SetActive(true);
            }
        }
    }

    private void InitializeWorld()
    {
        if (string.IsNullOrEmpty(worldName))
        {
            Debug.LogError("World name is not set!");
            return;
        }

        if (createNewWorld)
        {
            WorldManager.CreateWorld(worldName);
        }
        WorldManager.UpdateLastPlayed(worldName);
    }

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(worldName))
        {
            Debug.LogWarning("World name should not be empty!");
        }
    }
} 