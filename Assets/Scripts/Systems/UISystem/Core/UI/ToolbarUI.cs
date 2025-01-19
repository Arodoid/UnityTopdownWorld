using UnityEngine;
using UISystem.API;
using UISystem.Core.Tools;
using WorldSystem.Data;

namespace UISystem.Core.UI
{
    public class ToolbarUI : MonoBehaviour
    {
        private UISystemAPI _uiSystemAPI;
        private const int BUTTON_WIDTH = 150;
        private const int BUTTON_HEIGHT = 30;
        private const int MARGIN = 10;
        private string[] _blockTypes = { "Stone", "Dirt", "Grass", "Wood" }; // Add your block types
        private int _currentBrushSize = 1;

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
            bool isBlockToolActive = _uiSystemAPI.GetActiveTool() == "BlockTool";
            GUI.backgroundColor = isBlockToolActive ? Color.cyan : Color.white;
            if (GUILayout.Button("Block Tool", GUILayout.Width(BUTTON_WIDTH)))
            {
                _uiSystemAPI.SetActiveTool(isBlockToolActive ? null : "BlockTool");
            }

            // Box Tool Button
            bool isBoxToolActive = _uiSystemAPI.GetActiveTool() == "BoxTool";
            GUI.backgroundColor = isBoxToolActive ? Color.cyan : Color.white;
            if (GUILayout.Button("Box Tool", GUILayout.Width(BUTTON_WIDTH)))
            {
                _uiSystemAPI.SetActiveTool(isBoxToolActive ? null : "BoxTool");
            }

            // Inspector Tool Button
            bool isInspectorToolActive = _uiSystemAPI.GetActiveTool() == "InspectorTool";
            GUI.backgroundColor = isInspectorToolActive ? Color.cyan : Color.white;
            if (GUILayout.Button("Inspector Tool", GUILayout.Width(BUTTON_WIDTH)))
            {
                _uiSystemAPI.SetActiveTool(isInspectorToolActive ? null : "InspectorTool");
            }

            GUI.backgroundColor = Color.white;
            
            // Show block tool controls only when block tool is active
            if (isBlockToolActive)
            {
                GUILayout.Space(20);
                
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
            // Show box tool controls when box tool is active
            else if (isBoxToolActive)
            {
                GUILayout.Space(20);
                
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

            GUILayout.EndHorizontal();
            
            // Show inspector info when inspector tool is active
            if (isInspectorToolActive)
            {
                GUILayout.Space(20);
                var inspectorTool = _uiSystemAPI.GetTool("InspectorTool") as InspectorTool;
                if (inspectorTool != null)
                {
                    GUILayout.Label(inspectorTool.GetInspectionInfo(), GUI.skin.box);
                }
            }
            
            GUILayout.EndArea();
        }
    }
} 