using UnityEngine;
using UISystem.API;
using UISystem.Core.Tools;
using UISystem.Core.Interactions;
using UISystem.Core.UI;
using WorldSystem.API;
using ZoneSystem.API;
using EntitySystem.API;

namespace UISystem.Core
{
    public class UISystemManager : MonoBehaviour
    {
        [SerializeField] private InputHandler inputHandler;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private ToolManager toolManager;
        [SerializeField] private ToolbarUI toolbarUI;
        
        private UISystemAPI _uiSystemAPI;
        private WorldCoordinateMapper _coordinateMapper;
        private WorldSystemAPI _worldAPI;
        private ZoneSystemAPI _zoneAPI;
        private EntitySystemAPI _entityAPI;

        public UISystemAPI UISystemAPI => _uiSystemAPI;

        public void Initialize(WorldSystemAPI worldAPI, ZoneSystemAPI zoneAPI, EntitySystemAPI entityAPI)
        {
            _worldAPI = worldAPI;
            _zoneAPI = zoneAPI;
            _entityAPI = entityAPI;
            
            // Create dependencies
            _coordinateMapper = new WorldCoordinateMapper(mainCamera, worldAPI);
            
            // Initialize tool manager
            if (toolManager == null)
            {
                toolManager = gameObject.AddComponent<ToolManager>();
            }
            toolManager.Initialize(worldAPI, zoneAPI, _coordinateMapper, entityAPI);
            
            // Initialize UI System API
            _uiSystemAPI = new UISystemAPI(inputHandler, toolManager);

            // Initialize toolbar UI
            if (toolbarUI == null)
            {
                toolbarUI = gameObject.AddComponent<ToolbarUI>();
            }
            toolbarUI.Initialize(_uiSystemAPI);

            Debug.Log("UISystemManager initialized with all system APIs");
        }

        private void OnDestroy()
        {
            if (_uiSystemAPI != null)
            {
                // Clean up if needed
            }
        }
    }
} 