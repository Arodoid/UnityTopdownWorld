using UnityEngine;
using System.Collections;
using WorldSystem;
using WorldSystem.Base;
using WorldSystem.API;
using UISystem.Core;
using ZoneSystem.API;
using ZoneSystem.Core;
using EntitySystem.API;
using EntitySystem.Core;
using System.Threading.Tasks;

namespace Game
{
    public class GameManager : MonoBehaviour
    {
        [Header("System References")]
        [SerializeField] private ChunkManager chunkManager;
        [SerializeField] private UISystemManager uiSystemManager;
        [SerializeField] private ZoneManager zoneManager;
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private JobSystemComponent jobSystem;
        
        [Header("World Settings")]
        [SerializeField] private string worldName;
        [SerializeField] private int worldSeed;
        [SerializeField] private bool createNewWorld;

        private WorldSystemAPI _worldAPI;
        private ZoneSystemAPI _zoneAPI;
        private EntitySystemAPI _entityAPI;
        private bool _isInitialized;

        public bool IsInitialized => _isInitialized;
        public WorldSystemAPI WorldAPI => _worldAPI;
        public ZoneSystemAPI ZoneAPI => _zoneAPI;
        public EntitySystemAPI EntityAPI => _entityAPI;

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

            // Initialize Zone System
            if (zoneManager == null)
            {
                zoneManager = gameObject.AddComponent<ZoneManager>();
            }
            _zoneAPI = new ZoneSystemAPI(zoneManager);

            // Initialize Entity System
            if (entityManager == null)
            {
                entityManager = gameObject.AddComponent<EntityManager>();
            }
            entityManager.Initialize(_worldAPI);
            _entityAPI = new EntitySystemAPI(entityManager, jobSystem);

            // Initialize UI System
            uiSystemManager.Initialize(_worldAPI, _zoneAPI, _entityAPI);
            
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