using UnityEngine;
using UnityEditor;
using Unity.Mathematics;
using WorldSystem.Generation;

#if UNITY_EDITOR
public class BiomeVisualizer : EditorWindow
{
    private WorldGenSettings settings;
    private int visualizationSize = 256;
    private Texture2D biomeMap;
    private bool showTemperature = true;
    private bool showHumidity = true;
    private bool showContinentalness = true;
    private Vector2 scrollPosition;
    private float previewScale = 1f;

    [MenuItem("Tools/Biome Visualizer")]
    public static void ShowWindow()
    {
        GetWindow<BiomeVisualizer>("Biome Visualizer");
    }

    void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        settings = (WorldGenSettings)EditorGUILayout.ObjectField("World Gen Settings", settings, typeof(WorldGenSettings), false);
        visualizationSize = EditorGUILayout.IntSlider("Preview Size", visualizationSize, 64, 512);
        previewScale = EditorGUILayout.Slider("Preview Scale", previewScale, 0.5f, 4f);

        EditorGUILayout.Space();
        showTemperature = EditorGUILayout.Toggle("Show Temperature", showTemperature);
        showHumidity = EditorGUILayout.Toggle("Show Humidity", showHumidity);
        showContinentalness = EditorGUILayout.Toggle("Show Continentalness", showContinentalness);

        if (GUILayout.Button("Generate Preview") && settings != null)
        {
            GeneratePreview();
        }

        EditorGUILayout.Space();

        if (biomeMap != null)
        {
            float scaledSize = visualizationSize * previewScale;
            Rect rect = GUILayoutUtility.GetRect(scaledSize, scaledSize);
            EditorGUI.DrawPreviewTexture(rect, biomeMap);

            // Draw biome legend
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Biome Legend:", EditorStyles.boldLabel);
            if (settings != null && settings.Biomes != null)
            {
                foreach (var biome in settings.Biomes)
                {
                    Color biomeColor = GetBiomeColor(biome);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUI.DrawRect(GUILayoutUtility.GetRect(20, 20), biomeColor);
                    EditorGUILayout.LabelField($"Biome {biome.BiomeId}: T:{biome.Temperature:F2} H:{biome.Humidity:F2} C:{biome.Continentalness:F2}");
                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void GeneratePreview()
    {
        if (settings == null || settings.Biomes == null || settings.Biomes.Length == 0)
            return;

        biomeMap = new Texture2D(visualizationSize, visualizationSize);
        biomeMap.filterMode = FilterMode.Point;

        using var biomesArray = new Unity.Collections.NativeArray<BiomeSettings>(settings.Biomes, Unity.Collections.Allocator.Temp);
        var biomeGen = new BiomeGenerator(settings.BiomeNoiseSettings, biomesArray, settings.BiomeBlendDistance);

        // Debug values
        float minWeight = float.MaxValue;
        float maxWeight = float.MinValue;

        // Calculate the world size we want to visualize
        float worldSize = 1000f; // Size in world units we want to show
        float step = worldSize / visualizationSize;

        for (int y = 0; y < visualizationSize; y++)
        {
            for (int x = 0; x < visualizationSize; x++)
            {
                // Convert visualization coordinates to world coordinates
                float2 worldPos = new float2(x * step, y * step);

                using var weights = new Unity.Collections.NativeArray<float>(settings.Biomes.Length, Unity.Collections.Allocator.Temp);
                biomeGen.GetBiomeWeights(worldPos, weights);

                // Track min/max weights for debugging
                for (int i = 0; i < weights.Length; i++)
                {
                    minWeight = math.min(minWeight, weights[i]);
                    maxWeight = math.max(maxWeight, weights[i]);
                }

                int dominantIndex = 0;
                float maxBiomeWeight = weights[0];
                for (int i = 1; i < weights.Length; i++)
                {
                    if (weights[i] > maxBiomeWeight)
                    {
                        maxBiomeWeight = weights[i];
                        dominantIndex = i;
                    }
                }

                Color biomeColor = GetBiomeColor(settings.Biomes[dominantIndex]);
                biomeMap.SetPixel(x, y, biomeColor);
            }
        }

        biomeMap.Apply();

        // Debug output
        Debug.Log($"Weight Range: {minWeight:F3} to {maxWeight:F3}");
        Debug.Log($"World Size: {worldSize}x{worldSize}, Step: {step}");
    }

    private Color GetBiomeColor(BiomeSettings biome)
    {
        Color color = Color.white;  // Start with white
        
        if (showTemperature || showHumidity || showContinentalness)
        {
            // Initialize with neutral values
            float r = 0.5f;
            float g = 0.5f;
            float b = 0.5f;

            // Only modify the channels that are enabled
            if (showTemperature)
                r = biome.Temperature;
            if (showHumidity)
                g = biome.Humidity;
            if (showContinentalness)
                b = biome.Continentalness;

            color = new Color(r, g, b, 1);
        }
        else
        {
            // If no parameters are selected, use unique colors per biome
            switch (biome.BiomeId)
            {
                case 0: // Deep Ocean
                    color = new Color(0.1f, 0.1f, 0.8f);
                    break;
                case 1: // Coastal
                    color = new Color(0.3f, 0.7f, 0.9f);
                    break;
                case 2: // Plains
                    color = new Color(0.4f, 0.8f, 0.3f);
                    break;
                case 3: // Mountains
                    color = new Color(0.7f, 0.7f, 0.7f);
                    break;
                case 4: // Forest
                    color = new Color(0.2f, 0.5f, 0.2f);
                    break;
                case 5: // Tundra
                    color = new Color(0.9f, 0.9f, 0.9f);
                    break;
                case 6: // Desert
                    color = new Color(0.9f, 0.8f, 0.2f);
                    break;
                default:
                    color = Color.magenta; // Error color
                    break;
            }
        }

        return color;
    }
}
#endif