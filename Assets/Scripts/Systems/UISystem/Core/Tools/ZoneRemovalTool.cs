using UnityEngine;
using Unity.Mathematics;
using UISystem.API;
using UISystem.Core.Selection;
using UISystem.Core.Interactions;
using ZoneSystem.API;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

namespace UISystem.Core.Tools
{
    public class ZoneRemovalTool : IUITool
    {
        private readonly ZoneSystemAPI _zoneAPI;
        private readonly BoxSelector _boxSelector;
        private string _currentZoneType;
        
        public string ToolId => "ZoneRemovalTool";

        public ZoneRemovalTool(ZoneSystemAPI zoneAPI, WorldCoordinateMapper coordinateMapper, GameObject toolsParent)
        {
            _zoneAPI = zoneAPI;
            var visualizerObject = new GameObject("ZoneRemovalVisualizer");
            visualizerObject.transform.SetParent(toolsParent.transform);
            var boxVisualizer = visualizerObject.AddComponent<BoxVisualizer>();
            _boxSelector = new BoxSelector(coordinateMapper, boxVisualizer);
            _boxSelector.OnSelectionFinished += HandleSelectionFinished;
            _currentZoneType = _zoneAPI.GetAvailableZoneTypes().First();
        }

        public void OnToolActivated()
        {
            Debug.Log("Zone removal tool activated");
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
            if (selection.IsValid)
            {
                await _zoneAPI.RemoveZone(selection.Start, selection.End, _currentZoneType);
            }
        }

        public void SetZoneType(string zoneType)
        {
            _currentZoneType = zoneType;
        }

        public string GetToolInfo()
        {
            return $"Removing {_currentZoneType} Zone\nClick and drag to define area";
        }

        public IEnumerable<string> GetAvailableZoneTypes()
        {
            return _zoneAPI.GetAvailableZoneTypes();
        }
    }
} 