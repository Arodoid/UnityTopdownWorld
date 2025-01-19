using System.Collections.Generic;
using UnityEngine;
using UISystem.API;
using UISystem.Core.Tools;
using UISystem.Core.Interactions;
using WorldSystem.API;

namespace UISystem.Core
{
    public class ToolManager : MonoBehaviour, IToolManager
    {
        private readonly Dictionary<string, IUITool> _tools = new Dictionary<string, IUITool>();
        private string _activeToolId;
        private WorldSystemAPI _worldAPI;
        private WorldCoordinateMapper _coordinateMapper;
        private BlockHighlighter _blockHighlighter;

        public void Initialize(WorldSystemAPI worldAPI, WorldCoordinateMapper coordinateMapper)
        {
            _worldAPI = worldAPI;
            _coordinateMapper = coordinateMapper;
            
            // Create block highlighter
            _blockHighlighter = gameObject.AddComponent<BlockHighlighter>();
            _blockHighlighter.Initialize(_coordinateMapper, worldAPI);
            
            // Create and register default tools
            RegisterDefaultTools();
        }

        private void RegisterDefaultTools()
        {
            var blockTool = new BlockTool(_worldAPI, _coordinateMapper, _blockHighlighter);
            RegisterTool(blockTool);
            
            var inspectorTool = new InspectorTool(_worldAPI, _coordinateMapper, _blockHighlighter);
            RegisterTool(inspectorTool);
            
            var boxTool = new BoxSelectionTool(_worldAPI, _coordinateMapper, gameObject);
            RegisterTool(boxTool);
        }

        public void RegisterTool(IUITool tool)
        {
            _tools[tool.ToolId] = tool;
            Debug.Log($"Registered tool: {tool.ToolId}");
        }

        public void UnregisterTool(string toolId)
        {
            if (_activeToolId == toolId)
            {
                SetActiveTool(null);
            }
            _tools.Remove(toolId);
        }

        public void SetActiveTool(string toolId)
        {
            if (_activeToolId == toolId) return;

            // Deactivate current tool
            if (_activeToolId != null && _tools.TryGetValue(_activeToolId, out var currentTool))
            {
                currentTool.OnToolDeactivated();
            }

            _activeToolId = toolId;

            // Activate new tool
            if (toolId != null && _tools.TryGetValue(toolId, out var newTool))
            {
                newTool.OnToolActivated();
            }
        }

        public string GetActiveToolId() => _activeToolId;
        public bool IsToolActive(string toolId) => _activeToolId == toolId;

        public IUITool GetTool(string toolId)
        {
            if (_tools.TryGetValue(toolId, out var tool))
            {
                return tool;
            }
            return null;
        }
    }
} 