using UnityEngine;
using System.Collections;
using WorldSystem;
using WorldSystem.Implementation;
using EntitySystem.Core;
using WorldSystem.Base;
using WorldSystem.Generation;

public class GameManager : MonoBehaviour
{
    [Header("System References")]
    [SerializeField] private EntityManager entityManager;
    [SerializeField] private ChunkManager chunkManager;
    
    [Header("World Settings")]
    [SerializeField] private string worldName = "TestWorld";
    [SerializeField] private bool createNewWorld = true;
    [SerializeField] private WorldGenSettings worldGenSettings;

    private IWorldSystem _worldSystem;
    private bool _isInitialized;

    public bool IsInitialized => _isInitialized;
    public IWorldSystem WorldSystem => _worldSystem;

    private void Start()
    {
        if (!ValidateReferences()) return;
        
        InitializeWorld();
        StartCoroutine(InitializeSystems());
    }

    private bool ValidateReferences()
    {
        if (chunkManager == null)
        {
            Debug.LogError("ChunkManager reference is missing!");
            return false;
        }
        if (worldGenSettings == null)
        {
            Debug.LogError("WorldGenSettings reference is missing!");
            return false;
        }
        if (entityManager == null)
        {
            Debug.LogError("EntityManager reference is missing!");
            return false;
        }
        return true;
    }

    private IEnumerator InitializeSystems()
    {
        // Create world system with existing ChunkManager
        _worldSystem = new WorldSystemImpl(worldGenSettings, chunkManager);
        
        // Load the world
        var loadTask = _worldSystem.LoadWorld(worldName);
        while (!loadTask.IsCompleted)
        {
            yield return null;
        }

        if (!loadTask.Result)
        {
            Debug.LogError("Failed to load world!");
            yield break;
        }
        
        // Initialize entity system
        entityManager.Initialize(_worldSystem);
        
        _isInitialized = true;
        Debug.Log("Game systems initialized successfully!");
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