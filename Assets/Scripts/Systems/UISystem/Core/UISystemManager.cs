using UnityEngine;
using UISystem.API;
using UISystem.Core.Tools;
using UISystem.Core.Interactions;
using UISystem.Core.UI;
using WorldSystem.API;

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

        public UISystemAPI UISystemAPI => _uiSystemAPI;

        public void Initialize(WorldSystemAPI worldAPI)
        {
            // Create dependencies
            _coordinateMapper = new WorldCoordinateMapper(mainCamera, worldAPI);
            
            // Initialize tool manager
            if (toolManager == null)
            {
                toolManager = gameObject.AddComponent<ToolManager>();
            }
            toolManager.Initialize(worldAPI, _coordinateMapper);
            
            // Initialize UI System API
            _uiSystemAPI = new UISystemAPI(inputHandler, toolManager);

            // Initialize toolbar UI
            if (toolbarUI == null)
            {
                toolbarUI = gameObject.AddComponent<ToolbarUI>();
            }
            toolbarUI.Initialize(_uiSystemAPI);

            Debug.Log("UISystemManager initialized successfully");
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