using UnityEngine;
using Unity.Mathematics;
using UISystem.API;
using UISystem.Core.Interactions;
using UISystem.Core.Selection;
using WorldSystem.API;
using WorldSystem.Data;
using System.Threading.Tasks;

namespace UISystem.Core.Tools
{
    public class InspectorTool : IUITool
    {
        private readonly WorldSystemAPI _worldAPI;
        private readonly SingleBlockSelector _blockSelector;
        private BlockType? _lastInspectedBlockType;
        
        public string ToolId => "InspectorTool";

        public InspectorTool(WorldSystemAPI worldAPI, WorldCoordinateMapper coordinateMapper, BlockHighlighter blockHighlighter)
        {
            _worldAPI = worldAPI;
            _blockSelector = new SingleBlockSelector(coordinateMapper, blockHighlighter);
            _blockSelector.OnSelectionChanged += HandleSelectionChanged;
        }

        private async void HandleSelectionChanged(SingleBlockSelectionResult selection)
        {
            if (selection.IsValid)
            {
                _lastInspectedBlockType = await _worldAPI.GetBlockType(selection.Position);
            }
            else
            {
                _lastInspectedBlockType = null;
            }
        }

        public void OnToolActivated()
        {
            Debug.Log("Inspector tool activated");
            _blockSelector.ShowSelection();
        }

        public void OnToolDeactivated()
        {
            _blockSelector.HideSelection();
            _lastInspectedBlockType = null;
        }

        public void OnPointerMoved(Vector2 position)
        {
            _blockSelector.UpdateSelection(position);
        }

        public void OnPointerDown(Vector2 position) { }
        public void OnPointerUp(Vector2 position) { }
        public void OnPointerDragged(Vector2 position) { }

        public string GetInspectionInfo()
        {
            var selection = _blockSelector.GetCurrentSelection();
            if (selection?.IsValid == true && _lastInspectedBlockType.HasValue)
            {
                var pos = selection.Position;
                return $"Position: ({pos.x}, {pos.y}, {pos.z})\n" +
                       $"Block Type: {_lastInspectedBlockType.Value}\n" +
                       $"Height Level: {pos.y}";
            }
            return "No block selected";
        }
    }
} 