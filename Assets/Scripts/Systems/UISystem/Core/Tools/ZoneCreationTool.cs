using UnityEngine;
using Unity.Mathematics;
using UISystem.API;
using UISystem.Core.Selection;
using UISystem.Core.Interactions;
using ZoneSystem.API;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace UISystem.Core.Tools
{
    public class ZoneCreationTool : IUITool
    {
        private readonly ZoneSystemAPI _zoneAPI;
        private readonly BoxSelector _boxSelector;
        private string _currentZoneType;
        
        public string ToolId => "ZoneCreationTool";

        public ZoneCreationTool(ZoneSystemAPI zoneAPI, WorldCoordinateMapper coordinateMapper, GameObject toolsParent)
        {
            _zoneAPI = zoneAPI;
            var visualizerObject = new GameObject("ZoneCreationVisualizer");
            visualizerObject.transform.SetParent(toolsParent.transform);
            var boxVisualizer = visualizerObject.AddComponent<BoxVisualizer>();
            _boxSelector = new BoxSelector(coordinateMapper, boxVisualizer);
            _boxSelector.OnSelectionFinished += HandleSelectionFinished;
            _currentZoneType = _zoneAPI.GetAvailableZoneTypes().First();
        }

        public void OnToolActivated()
        {
            Debug.Log("ZoneCreationTool: Tool activated");
        }

        public void OnToolDeactivated()
        {
            _boxSelector.CancelSelection();
        }

        public void OnPointerDown(Vector2 position)
        {
            _boxSelector.StartSelection(position);
        }

        public void OnPointerUp(Vector2 position)
        {
            _boxSelector.FinishSelection(position);
        }

        public void OnPointerMoved(Vector2 position)
        {
            _boxSelector.UpdateSelection(position);
        }

        public void OnPointerDragged(Vector2 position)
        {
            _boxSelector.UpdateSelection(position);
        }

        private async void HandleSelectionFinished(BoxSelectionResult selection)
        {
            Debug.Log("ZoneCreationTool: HandleSelectionFinished called");
            Debug.Log($"ZoneCreationTool: Got selection: {selection != null}, IsValid: {selection?.IsValid}");
            
            if (selection != null && selection.IsValid)
            {
                Debug.Log($"ZoneCreationTool: Creating zone of type {_currentZoneType} from {selection.Start} to {selection.End}");
                bool success = await _zoneAPI.CreateZone(selection.Start, selection.End, _currentZoneType);
                Debug.Log($"ZoneCreationTool: Zone creation {(success ? "succeeded" : "failed")}");
            }
            else
            {
                Debug.Log($"ZoneCreationTool: Selection was invalid. Selection: {selection}, IsValid: {selection?.IsValid}");
            }
        }

        public void SetZoneType(string zoneType)
        {
            _currentZoneType = zoneType;
        }

        public string GetToolInfo()
        {
            return $"Creating {_currentZoneType} Zone\nClick and drag to define area";
        }

        public IEnumerable<string> GetAvailableZoneTypes()
        {
            return _zoneAPI.GetAvailableZoneTypes();
        }
    }
} 