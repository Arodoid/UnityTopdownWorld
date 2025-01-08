using UnityEngine;
using WorldSystem.Base;
using WorldSystem.Generation;

public class WorldSettingsDebugUI : MonoBehaviour
{
    [SerializeField] private ChunkManager chunkManager;
    [SerializeField] private WorldGenerationSettings settings;
    [SerializeField] private KeyCode toggleKey = KeyCode.F1;
    
    private bool showWindow = false;
    private Vector2 scrollPosition;
    private Rect windowRect = new Rect(20, 20, 350, 1500);

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            showWindow = !showWindow;
        }
    }

    private void OnGUI()
    {
        if (!showWindow) return;

        GUI.skin.verticalScrollbar.fixedWidth = 20;
        GUI.skin.verticalScrollbarThumb.fixedWidth = 20;
        
        windowRect = GUILayout.Window(0, windowRect, DrawWindow, "World Generation Settings");
    }

    private void DrawWindow(int windowID)
    {
        if (settings == null || chunkManager == null)
        {
            GUILayout.Label("Settings or ChunkManager not assigned!");
            GUI.DragWindow();
            return;
        }

        GUILayout.Space(10);
        GUILayout.BeginVertical();
        scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));

        GUILayout.Label("World Seed", EditorStyles.boldLabel);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Seed", GUILayout.Width(150));
        string seedStr = GUILayout.TextField(settings.seed.ToString(), GUILayout.Width(100));
        if (int.TryParse(seedStr, out int newSeed))
        {
            settings.seed = newSeed;
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(10);
        GUILayout.Label("Noise Scales", EditorStyles.boldLabel);
        settings.continentScale = EditorSlider("Continent Scale", settings.continentScale, 0.0001f, 0.01f);
        settings.temperatureScale = EditorSlider("Temperature Scale", settings.temperatureScale, 0.0001f, 0.01f);
        settings.moistureScale = EditorSlider("Moisture Scale", settings.moistureScale, 0.0001f, 0.01f);
        settings.weirdnessScale = EditorSlider("Weirdness Scale", settings.weirdnessScale, 0.0001f, 0.01f);
        settings.erosionScale = EditorSlider("Erosion Scale", settings.erosionScale, 0.0001f, 0.01f);
        settings.localVariationScale = EditorSlider("Local Variation Scale", settings.localVariationScale, 0.0001f, 0.1f);

        GUILayout.Space(10);
        GUILayout.Label("Height Settings", EditorStyles.boldLabel);
        settings.waterLevel = EditorIntSlider("Water Level", settings.waterLevel, 0, 255);
        settings.oceanFloorMin = EditorIntSlider("Ocean Floor Min", settings.oceanFloorMin, 0, 255);
        settings.oceanFloorMax = EditorIntSlider("Ocean Floor Max", settings.oceanFloorMax, 0, 255);
        settings.mountainHeight = EditorIntSlider("Mountain Height", settings.mountainHeight, 0, 255);

        GUILayout.Space(10);
        GUILayout.Label("Biome Thresholds", EditorStyles.boldLabel);
        settings.oceanThreshold = EditorSlider("Ocean Threshold", settings.oceanThreshold, 0f, 1f);
        settings.mountainThreshold = EditorSlider("Mountain Threshold", settings.mountainThreshold, 0f, 1f);
        settings.forestThreshold = EditorSlider("Forest Threshold", settings.forestThreshold, 0f, 1f);

        GUILayout.Space(10);
        GUILayout.Label("Variation Settings", EditorStyles.boldLabel);
        settings.localVariationStrength = EditorSlider("Local Variation", settings.localVariationStrength, 0f, 50f);
        settings.mountainVariationStrength = EditorSlider("Mountain Variation", settings.mountainVariationStrength, 0f, 50f);
        settings.forestVariationStrength = EditorSlider("Forest Variation", settings.forestVariationStrength, 0f, 20f);
        settings.plainsVariationStrength = EditorSlider("Plains Variation", settings.plainsVariationStrength, 0f, 20f);

        GUILayout.Space(10);
        GUILayout.Label("Erosion Settings", EditorStyles.boldLabel);
        settings.erosionStrength = EditorSlider("Erosion Strength", settings.erosionStrength, 0f, 1f);
        settings.erosionDetailInfluence = EditorSlider("Erosion Detail", settings.erosionDetailInfluence, 0f, 1f);

        GUILayout.Space(20);
        if (GUILayout.Button("Apply Changes"))
        {
            chunkManager.UpdateWorldSettings(settings);
        }

        GUILayout.EndScrollView();
        GUILayout.EndVertical();
        GUI.DragWindow();
    }

    private float EditorSlider(string label, float value, float min, float max)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(150));
        float newValue = GUILayout.HorizontalSlider(value, min, max);
        GUILayout.Label(newValue.ToString("F4"), GUILayout.Width(70));
        GUILayout.EndHorizontal();
        return newValue;
    }

    private int EditorIntSlider(string label, int value, int min, int max)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(150));
        int newValue = (int)GUILayout.HorizontalSlider(value, min, max);
        GUILayout.Label(newValue.ToString(), GUILayout.Width(70));
        GUILayout.EndHorizontal();
        return newValue;
    }

    private static class EditorStyles
    {
        private static GUIStyle _boldLabel;
        public static GUIStyle boldLabel
        {
            get
            {
                if (_boldLabel == null)
                {
                    _boldLabel = new GUIStyle(GUI.skin.label)
                    {
                        fontStyle = FontStyle.Bold
                    };
                }
                return _boldLabel;
            }
        }
    }
} 