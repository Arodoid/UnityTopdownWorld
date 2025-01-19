using UnityEngine;
using Unity.Mathematics;
using UISystem.API;
using UISystem.Core.Interactions;
using WorldSystem.API;
using WorldSystem.Data;
using System.Threading.Tasks;

namespace UISystem.Core.Tools
{
    public class InspectorTool : IUITool
    {
        private readonly BlockHighlighter _blockHighlighter;
        private readonly WorldSystemAPI _worldAPI;
        private readonly WorldCoordinateMapper _coordinateMapper;
        private int3? _lastInspectedPosition;
        private BlockType? _lastInspectedBlockType;
        
        public string ToolId => "InspectorTool";

        public InspectorTool(WorldSystemAPI worldAPI, WorldCoordinateMapper coordinateMapper, BlockHighlighter blockHighlighter)
        {
            _worldAPI = worldAPI;
            _coordinateMapper = coordinateMapper;
            _blockHighlighter = blockHighlighter;
        }

        public void OnToolActivated()
        {
            Debug.Log("Inspector tool activated");
            _blockHighlighter.ShowHighlight();
        }

        public void OnToolDeactivated()
        {
            _blockHighlighter.HideHighlight();
            _lastInspectedPosition = null;
            _lastInspectedBlockType = null;
        }

        public async void OnPointerMoved(Vector2 position)
        {
            _blockHighlighter.UpdateHighlight(position);
            await UpdateInspection();
        }

        public void OnPointerDown(Vector2 position) { }
        public void OnPointerUp(Vector2 position) { }
        public void OnPointerDragged(Vector2 position) { }

        private async Task UpdateInspection()
        {
            if (_blockHighlighter.GetHighlightedPosition() is int3 blockPos)
            {
                _lastInspectedPosition = blockPos;
                _lastInspectedBlockType = await _worldAPI.GetBlockType(blockPos);
            }
            else
            {
                _lastInspectedPosition = null;
                _lastInspectedBlockType = null;
            }
        }

        public string GetInspectionInfo()
        {
            if (_lastInspectedPosition.HasValue && _lastInspectedBlockType.HasValue)
            {
                var pos = _lastInspectedPosition.Value;
                return $"Position: ({pos.x}, {pos.y}, {pos.z})\n" +
                       $"Block Type: {_lastInspectedBlockType.Value}\n" +
                       $"Height Level: {pos.y}";
            }
            return "No block selected";
        }
    }
} 