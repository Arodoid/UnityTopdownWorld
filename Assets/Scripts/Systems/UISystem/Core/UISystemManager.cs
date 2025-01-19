using UnityEngine;
using UISystem.API;
using UISystem.Core.Interactions;
using WorldSystem.API;

namespace UISystem.Core
{
    public class UISystemManager : MonoBehaviour
    {
        [SerializeField] private InputHandler inputHandler;
        [SerializeField] private Camera mainCamera;
        
        private UISystemAPI _uiSystemAPI;
        private WorldCoordinateMapper _coordinateMapper;
        private BlockHighlighter _blockHighlighter;
        private IToolManager _toolManager; // We'll implement this later

        public UISystemAPI UISystemAPI => _uiSystemAPI;

        public void Initialize(WorldSystemAPI worldAPI)
        {
            // Create dependencies
            _coordinateMapper = new WorldCoordinateMapper(mainCamera, worldAPI);
            _toolManager = new DefaultToolManager(); // Temporary simple implementation
            
            // Initialize UI System API
            _uiSystemAPI = new UISystemAPI(inputHandler, _toolManager);

            // Setup block highlighter
            _blockHighlighter = gameObject.AddComponent<BlockHighlighter>();
            _blockHighlighter.Initialize(_coordinateMapper, worldAPI);

            // Subscribe to input events
            _uiSystemAPI.OnPointerMoved += HandlePointerMoved;
        }

        private void HandlePointerMoved(Vector2 position)
        {
            if (_blockHighlighter != null)
            {
                _blockHighlighter.UpdateHighlight(position);
            }
        }

        private void OnDestroy()
        {
            if (_uiSystemAPI != null)
            {
                _uiSystemAPI.OnPointerMoved -= HandlePointerMoved;
            }
        }
    }

    // Temporary simple implementation of IToolManager
    public class DefaultToolManager : IToolManager
    {
        private string _activeToolId;
        
        public void SetActiveTool(string toolId) => _activeToolId = toolId;
        public string GetActiveToolId() => _activeToolId;
        public bool IsToolActive(string toolId) => _activeToolId == toolId;
        public void RegisterTool(IUITool tool) { }
        public void UnregisterTool(string toolId) { }
    }
} 