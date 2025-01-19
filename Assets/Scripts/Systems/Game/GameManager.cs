using UnityEngine;
using System.Collections;
using WorldSystem;
using WorldSystem.Base;
using WorldSystem.API;
using UISystem.Core;
using System.Threading.Tasks;

namespace Game
{
    public class GameManager : MonoBehaviour
    {
        [Header("System References")]
        [SerializeField] private ChunkManager chunkManager;
        [SerializeField] private UISystemManager uiSystemManager;
        
        [Header("World Settings")]
        [SerializeField] private string worldName;
        [SerializeField] private int worldSeed;
        [SerializeField] private bool createNewWorld;

        private WorldSystemAPI _worldAPI;
        private bool _isInitialized;

        public bool IsInitialized => _isInitialized;
        public WorldSystemAPI WorldAPI => _worldAPI;

        private void Start()
        {
            StartCoroutine(InitializeGameSystems());
        }

        private IEnumerator InitializeGameSystems()
        {
            InitializeWorld();
            
            _worldAPI = new WorldSystemAPI(chunkManager, worldSeed);
            
            // Handle async world loading
            var loadTask = _worldAPI.LoadWorld(worldName);
            while (!loadTask.IsCompleted)
            {
                yield return null;
            }

            if (!loadTask.Result)
            {
                Debug.LogError("Failed to load world!");
                yield break;
            }

            // Initialize UI System
            uiSystemManager.Initialize(_worldAPI);
            
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
                WorldManager.CreateWorld(worldName, worldSeed);
            }
            WorldManager.UpdateLastPlayed(worldName);
        }
    } 
}