using UnityEngine;
using UnityEngine.UI;
using UISystem.API;
using UISystem.Core.Tools;
using WorldSystem.Data;
using TMPro;
using System.Linq;

namespace UISystem.Core.UI
{
    public class ToolbarUI : MonoBehaviour
    {
        [SerializeField] private Canvas _canvas;
        [SerializeField] private RectTransform _toolbarPanel;
        [SerializeField] private RectTransform _controlsPanel;
        [SerializeField] private GameObject _buttonPrefab;
        
        private UISystemAPI _uiSystemAPI;
        private const float BUTTON_WIDTH = 100f;
        private const float BUTTON_HEIGHT = 30f;
        private const float MARGIN = 5f;
        private string[] _blockTypes = { "Stone", "Dirt", "Grass", "Wood" };
        private int _currentBrushSize = 1;
        private TMP_Dropdown _entityDropdown;

        public void Initialize(UISystemAPI uiSystemAPI)
        {
            _uiSystemAPI = uiSystemAPI;
            
            if (_canvas == null)
            {
                // Create canvas if not assigned
                var canvasObj = new GameObject("ToolbarCanvas");
                _canvas = canvasObj.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            if (_toolbarPanel == null)
            {
                // Create main panel
                var panelObj = new GameObject("ToolbarPanel");
                _toolbarPanel = panelObj.AddComponent<RectTransform>();
                var layout = panelObj.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = MARGIN;
                layout.padding = new RectOffset(10, 10, 5, 5);
                _toolbarPanel.SetParent(_canvas.transform, false);
                
                // Position at top of screen
                _toolbarPanel.anchorMin = new Vector2(0, 1);
                _toolbarPanel.anchorMax = new Vector2(1, 1);
                _toolbarPanel.pivot = new Vector2(0.5f, 1);
                _toolbarPanel.sizeDelta = new Vector2(0, BUTTON_HEIGHT + MARGIN * 2);
            }

            if (_controlsPanel == null)
            {
                var controlsObj = new GameObject("ControlsPanel");
                _controlsPanel = controlsObj.AddComponent<RectTransform>();
                var layout = controlsObj.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = MARGIN;
                layout.padding = new RectOffset(10, 10, 5, 5);
                _controlsPanel.SetParent(_canvas.transform, false);
                
                _controlsPanel.anchorMin = new Vector2(0, 1);
                _controlsPanel.anchorMax = new Vector2(1, 1);
                _controlsPanel.pivot = new Vector2(0.5f, 1);
                _controlsPanel.anchoredPosition = new Vector2(0, -(BUTTON_HEIGHT + MARGIN * 4));
                _controlsPanel.sizeDelta = new Vector2(0, BUTTON_HEIGHT + MARGIN * 2);
            }

            if (_buttonPrefab == null)
            {
                // Create button prefab
                _buttonPrefab = CreateButtonPrefab();
            }

            CreateToolButtons();
            UpdateControlsPanel();
        }

        private GameObject CreateButtonPrefab()
        {
            var buttonObj = new GameObject("ButtonPrefab", typeof(RectTransform));
            var button = buttonObj.AddComponent<Button>();
            var image = buttonObj.AddComponent<Image>();
            
            // Setup button visuals
            var colors = button.colors;
            colors.normalColor = new Color(0.8f, 0.8f, 0.8f, 1f); // Light gray
            colors.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            colors.selectedColor = new Color(0.6f, 0.8f, 1f, 1f); // Light blue
            button.colors = colors;
            
            // Add text
            var textObj = new GameObject("Text", typeof(RectTransform));
            var text = textObj.AddComponent<TextMeshProUGUI>();
            textObj.transform.SetParent(buttonObj.transform, false);
            
            // Setup text
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 12;
            text.color = Color.black;
            
            // Setup rect transforms
            var buttonRect = buttonObj.GetComponent<RectTransform>();
            buttonRect.sizeDelta = new Vector2(BUTTON_WIDTH, BUTTON_HEIGHT);
            
            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            
            buttonObj.SetActive(false);
            return buttonObj;
        }

        private void CreateToolButtons()
        {
            CreateToolButton("BlockTool", "Block Tool");
            CreateToolButton("BoxTool", "Box Tool");
            CreateToolButton("InspectorTool", "Inspector Tool");
            CreateToolButton("ZoneCreationTool", "Create Zone");
            CreateToolButton("ZoneRemovalTool", "Remove Zone");
            CreateToolButton("EntitySpawnTool", "Spawn Entity");
            CreateToolButton("MiningTool", "Mining Tool");
        }

        private void CreateToolButton(string toolId, string label)
        {
            var buttonObj = Instantiate(_buttonPrefab, _toolbarPanel);
            buttonObj.SetActive(true);
            
            var button = buttonObj.GetComponent<Button>();
            var text = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
            text.text = label;
            
            button.onClick.AddListener(() => {
                bool isToolActive = _uiSystemAPI.GetActiveTool() == toolId;
                _uiSystemAPI.SetActiveTool(isToolActive ? null : toolId);
                UpdateButtonStates();
            });
        }

        private void UpdateButtonStates()
        {
            string activeTool = _uiSystemAPI.GetActiveTool();
            
            foreach (var button in _toolbarPanel.GetComponentsInChildren<Button>())
            {
                var text = button.GetComponentInChildren<TextMeshProUGUI>();
                bool isActive = GetToolIdFromLabel(text.text) == activeTool;
                button.image.color = isActive ? button.colors.selectedColor : button.colors.normalColor;
            }

            UpdateControlsPanel();
        }

        private string GetToolIdFromLabel(string label)
        {
            return label switch
            {
                "Block Tool" => "BlockTool",
                "Box Tool" => "BoxTool",
                "Inspector Tool" => "InspectorTool",
                "Create Zone" => "ZoneCreationTool",
                "Remove Zone" => "ZoneRemovalTool",
                "Spawn Entity" => "EntitySpawnTool",
                "Mining Tool" => "MiningTool",
                _ => ""
            };
        }

        private void UpdateControlsPanel()
        {
            // Clear existing controls
            foreach (Transform child in _controlsPanel)
            {
                Destroy(child.gameObject);
            }

            string activeTool = _uiSystemAPI.GetActiveTool();
            if (string.IsNullOrEmpty(activeTool)) return;

            switch (activeTool)
            {
                case "BlockTool":
                case "BoxTool":
                    CreateBlockControls();
                    break;
                case "EntitySpawnTool":
                    CreateEntityControls();
                    break;
                case "ZoneCreationTool":
                case "ZoneRemovalTool":
                    CreateZoneControls();
                    break;
            }
        }

        private void CreateBlockControls()
        {
            foreach (string blockType in _blockTypes)
            {
                var buttonObj = Instantiate(_buttonPrefab, _controlsPanel);
                buttonObj.SetActive(true);
                
                var button = buttonObj.GetComponent<Button>();
                var text = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
                text.text = blockType;
                
                button.onClick.AddListener(() => {
                    var tool = _uiSystemAPI.GetActiveTool();
                    if (tool == "BlockTool")
                    {
                        var blockTool = _uiSystemAPI.GetTool("BlockTool") as BlockTool;
                        blockTool?.SetBlockType((BlockType)System.Enum.Parse(typeof(BlockType), blockType));
                    }
                    else if (tool == "BoxTool")
                    {
                        var boxTool = _uiSystemAPI.GetTool("BoxTool") as BoxSelectionTool;
                        boxTool?.SetBlockType((BlockType)System.Enum.Parse(typeof(BlockType), blockType));
                    }
                });
            }

            if (_uiSystemAPI.GetActiveTool() == "BlockTool")
            {
                CreateBrushSizeControls();
            }
        }

        private void CreateBrushSizeControls()
        {
            var container = new GameObject("BrushControls", typeof(RectTransform));
            var layout = container.AddComponent<HorizontalLayoutGroup>();
            container.transform.SetParent(_controlsPanel, false);

            // Minus button
            var minusBtn = Instantiate(_buttonPrefab, container.transform);
            minusBtn.SetActive(true);
            minusBtn.GetComponentInChildren<TextMeshProUGUI>().text = "-";
            minusBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(30, BUTTON_HEIGHT);
            
            // Size display
            var sizeText = Instantiate(_buttonPrefab, container.transform);
            sizeText.SetActive(true);
            sizeText.GetComponent<Button>().enabled = false;
            sizeText.GetComponentInChildren<TextMeshProUGUI>().text = _currentBrushSize.ToString();
            sizeText.GetComponent<RectTransform>().sizeDelta = new Vector2(30, BUTTON_HEIGHT);
            
            // Plus button
            var plusBtn = Instantiate(_buttonPrefab, container.transform);
            plusBtn.SetActive(true);
            plusBtn.GetComponentInChildren<TextMeshProUGUI>().text = "+";
            plusBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(30, BUTTON_HEIGHT);

            minusBtn.GetComponent<Button>().onClick.AddListener(() => {
                _currentBrushSize = Mathf.Max(1, _currentBrushSize - 2);
                sizeText.GetComponentInChildren<TextMeshProUGUI>().text = _currentBrushSize.ToString();
                var tool = _uiSystemAPI.GetTool("BlockTool") as BlockTool;
                tool?.SetBrushSize(_currentBrushSize);
            });

            plusBtn.GetComponent<Button>().onClick.AddListener(() => {
                _currentBrushSize = Mathf.Min(5, _currentBrushSize + 2);
                sizeText.GetComponentInChildren<TextMeshProUGUI>().text = _currentBrushSize.ToString();
                var tool = _uiSystemAPI.GetTool("BlockTool") as BlockTool;
                tool?.SetBrushSize(_currentBrushSize);
            });
        }

        private void CreateEntityControls()
        {
            var spawnTool = _uiSystemAPI.GetTool("EntitySpawnTool") as EntitySpawnTool;
            if (spawnTool?.AvailableEntities == null) return;

            foreach (var entityId in spawnTool.AvailableEntities)
            {
                var buttonObj = Instantiate(_buttonPrefab, _controlsPanel);
                buttonObj.SetActive(true);
                
                var button = buttonObj.GetComponent<Button>();
                var text = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
                text.text = entityId;
                
                button.onClick.AddListener(() => {
                    spawnTool.SetSelectedEntity(entityId);
                    UpdateEntityButtons();
                });
            }
        }

        private void UpdateEntityButtons()
        {
            var spawnTool = _uiSystemAPI.GetTool("EntitySpawnTool") as EntitySpawnTool;
            if (spawnTool == null) return;

            foreach (var button in _controlsPanel.GetComponentsInChildren<Button>())
            {
                var text = button.GetComponentInChildren<TextMeshProUGUI>();
                bool isSelected = text.text == spawnTool.SelectedEntityId;
                button.image.color = isSelected ? button.colors.selectedColor : button.colors.normalColor;
            }
        }

        private void CreateZoneControls()
        {
            var zoneAPI = _uiSystemAPI.GetTool("ZoneCreationTool") as ZoneCreationTool;
            if (zoneAPI == null) return;

            foreach (var zoneType in zoneAPI.GetAvailableZoneTypes())
            {
                var buttonObj = Instantiate(_buttonPrefab, _controlsPanel);
                buttonObj.SetActive(true);
                
                var button = buttonObj.GetComponent<Button>();
                var text = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
                text.text = zoneType.ToString();
                
                button.onClick.AddListener(() => {
                    var creationTool = _uiSystemAPI.GetTool("ZoneCreationTool") as ZoneCreationTool;
                    var removalTool = _uiSystemAPI.GetTool("ZoneRemovalTool") as ZoneRemovalTool;
                    
                    creationTool?.SetZoneType(zoneType);
                    removalTool?.SetZoneType(zoneType);
                });
            }
        }
    }
} 