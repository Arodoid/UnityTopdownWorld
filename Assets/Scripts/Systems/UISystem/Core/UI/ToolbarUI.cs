using UnityEngine;
using UISystem.API;
using UISystem.Core.Tools;
using WorldSystem.Data;
using TMPro;
using System.Linq;

namespace UISystem.Core.UI
{
    public class ToolbarUI : MonoBehaviour
    {
        private UISystemAPI _uiSystemAPI;
        private const int BUTTON_WIDTH = 150;
        private const int BUTTON_HEIGHT = 30;
        private const int MARGIN = 10;
        private string[] _blockTypes = { "Stone", "Dirt", "Grass", "Wood" };
        private int _currentBrushSize = 1;
        private TMP_Dropdown _entityDropdown;

        public void Initialize(UISystemAPI uiSystemAPI)
        {
            _uiSystemAPI = uiSystemAPI;
        }

        private void OnGUI()
        {
            if (_uiSystemAPI == null) return;

            GUILayout.BeginArea(new Rect(MARGIN, MARGIN, Screen.width - MARGIN * 2, Screen.height - MARGIN * 2));
            
            // Top toolbar
            GUILayout.BeginHorizontal();
            
            // Block Tool Button
            DrawToolButton("BlockTool", "Block Tool");
            
            // Box Tool Button
            DrawToolButton("BoxTool", "Box Tool");
            
            // Inspector Tool Button
            DrawToolButton("InspectorTool", "Inspector Tool");

            // Zone Creation Tool Button
            DrawToolButton("ZoneCreationTool", "Create Zone");

            // Zone Removal Tool Button
            DrawToolButton("ZoneRemovalTool", "Remove Zone");

            // Entity Spawn Tool Button
            DrawToolButton("EntitySpawnTool", "Spawn Entity");

            GUI.backgroundColor = Color.white;
            
            // Show tool-specific controls
            GUILayout.Space(20);
            
            if (_uiSystemAPI.GetActiveTool() == "BlockTool")
            {
                DrawBlockToolControls();
            }
            else if (_uiSystemAPI.GetActiveTool() == "BoxTool")
            {
                DrawBoxToolControls();
            }
            else if (_uiSystemAPI.GetActiveTool() == "ZoneCreationTool" || 
                     _uiSystemAPI.GetActiveTool() == "ZoneRemovalTool")
            {
                DrawZoneToolControls();
            }
            else if (_uiSystemAPI.GetActiveTool() == "InspectorTool")
            {
                DrawInspectorInfo();
            }
            else if (_uiSystemAPI.GetActiveTool() == "EntitySpawnTool")
            {
                DrawEntitySpawnControls();
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawEntitySpawnControls()
        {
            var spawnTool = _uiSystemAPI.GetTool("EntitySpawnTool") as EntitySpawnTool;
            if (spawnTool == null)
            {
                Debug.LogError("EntitySpawnTool not found!");
                return;
            }

            if (spawnTool.AvailableEntities == null)
            {
                Debug.LogError("AvailableEntities is null!");
                return;
            }

            if (!spawnTool.AvailableEntities.Any())
            {
                Debug.LogError("No available entities found!");
                return;
            }

            GUILayout.BeginVertical();
            GUILayout.Label($"Available Entities: {spawnTool.AvailableEntities.Count}");
            
            foreach (var entityId in spawnTool.AvailableEntities)
            {
                bool isSelected = entityId == spawnTool.SelectedEntityId;
                GUI.backgroundColor = isSelected ? Color.cyan : Color.white;
                
                if (GUILayout.Button(entityId, GUILayout.Width(100)))
                {
                    spawnTool.SetSelectedEntity(entityId);
                }
            }

            GUI.backgroundColor = Color.white;
            GUILayout.Label(spawnTool.GetToolInfo());
            GUILayout.EndVertical();
        }

        private void DrawToolButton(string toolId, string label)
        {
            bool isToolActive = _uiSystemAPI.GetActiveTool() == toolId;
            GUI.backgroundColor = isToolActive ? Color.cyan : Color.white;
            if (GUILayout.Button(label, GUILayout.Width(BUTTON_WIDTH)))
            {
                _uiSystemAPI.SetActiveTool(isToolActive ? null : toolId);
            }
        }

        private void DrawBlockToolControls()
        {
            // Block Type Selection
            GUILayout.Label("Block Type:", GUILayout.Width(70));
            foreach (string blockType in _blockTypes)
            {
                if (GUILayout.Button(blockType, GUILayout.Width(70)))
                {
                    var tool = _uiSystemAPI.GetTool("BlockTool") as BlockTool;
                    tool?.SetBlockType((BlockType)System.Enum.Parse(typeof(BlockType), blockType));
                }
            }

            GUILayout.Space(20);
            
            // Brush Size Controls
            DrawBrushSizeControls();
        }

        private void DrawBoxToolControls()
        {
            // Block Type Selection for Box Tool
            GUILayout.Label("Block Type:", GUILayout.Width(70));
            foreach (string blockType in _blockTypes)
            {
                if (GUILayout.Button(blockType, GUILayout.Width(70)))
                {
                    var tool = _uiSystemAPI.GetTool("BoxTool") as BoxSelectionTool;
                    tool?.SetBlockType((BlockType)System.Enum.Parse(typeof(BlockType), blockType));
                }
            }

            // Show current selection info
            var boxTool = _uiSystemAPI.GetTool("BoxTool") as BoxSelectionTool;
            if (boxTool != null)
            {
                GUILayout.Space(10);
                GUILayout.Label(boxTool.GetSelectionInfo());
            }
        }

        private void DrawZoneToolControls()
        {
            GUILayout.Label("Zone Type:", GUILayout.Width(70));
            
            var zoneAPI = _uiSystemAPI.GetTool("ZoneCreationTool") as ZoneCreationTool;
            if (zoneAPI != null)
            {
                foreach (var zoneType in zoneAPI.GetAvailableZoneTypes())
                {
                    if (GUILayout.Button(zoneType.ToString(), GUILayout.Width(100)))
                    {
                        var creationTool = _uiSystemAPI.GetTool("ZoneCreationTool") as ZoneCreationTool;
                        var removalTool = _uiSystemAPI.GetTool("ZoneRemovalTool") as ZoneRemovalTool;
                        
                        creationTool?.SetZoneType(zoneType);
                        removalTool?.SetZoneType(zoneType);
                    }
                }
            }

            // Show current tool info
            string activeToolId = _uiSystemAPI.GetActiveTool();
            if (activeToolId == "ZoneCreationTool")
            {
                var tool = _uiSystemAPI.GetTool(activeToolId) as ZoneCreationTool;
                GUILayout.Label(tool?.GetToolInfo() ?? "");
            }
            else if (activeToolId == "ZoneRemovalTool")
            {
                var tool = _uiSystemAPI.GetTool(activeToolId) as ZoneRemovalTool;
                GUILayout.Label(tool?.GetToolInfo() ?? "");
            }
        }

        private void DrawInspectorInfo()
        {
            var inspectorTool = _uiSystemAPI.GetTool("InspectorTool") as InspectorTool;
            if (inspectorTool != null)
            {
                GUILayout.Label(inspectorTool.GetInspectionInfo(), GUI.skin.box);
            }
        }

        private void DrawBrushSizeControls()
        {
            GUILayout.Label("Brush Size:", GUILayout.Width(70));
            if (GUILayout.Button("-", GUILayout.Width(30)))
            {
                _currentBrushSize = Mathf.Max(1, _currentBrushSize - 2);
                var tool = _uiSystemAPI.GetTool("BlockTool") as BlockTool;
                tool?.SetBrushSize(_currentBrushSize);
            }
            GUILayout.Label(_currentBrushSize.ToString(), GUILayout.Width(30));
            if (GUILayout.Button("+", GUILayout.Width(30)))
            {
                _currentBrushSize = Mathf.Min(5, _currentBrushSize + 2);
                var tool = _uiSystemAPI.GetTool("BlockTool") as BlockTool;
                tool?.SetBrushSize(_currentBrushSize);
            }
        }
    }
} 