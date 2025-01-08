using UnityEngine;
using UnityEditor;
using WorldSystem.Generation;

public class NoiseVisualizerWindow : EditorWindow
{
    private NoiseVisualizer visualizer;
    private bool autoUpdate = true;

    [MenuItem("World/Noise Visualizer")]
    public static void ShowWindow()
    {
        GetWindow<NoiseVisualizerWindow>("Noise Visualizer");
    }

    private void OnGUI()
    {
        if (visualizer == null)
        {
            visualizer = FindFirstObjectByType<NoiseVisualizer>();
            if (visualizer == null)
            {
                if (GUILayout.Button("Create Visualizer"))
                {
                    GameObject go = new GameObject("NoiseVisualizer");
                    visualizer = go.AddComponent<NoiseVisualizer>();
                }
                return;
            }
        }

        EditorGUI.BeginChangeCheck();

        EditorGUILayout.LabelField("Noise Settings", EditorStyles.boldLabel);
        visualizer.noiseScale = EditorGUILayout.FloatField("Noise Scale", visualizer.noiseScale);
        visualizer.seed = EditorGUILayout.IntField("Seed", visualizer.seed);
        visualizer.octaves = EditorGUILayout.IntField("Octaves", visualizer.octaves);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Visualization Settings", EditorStyles.boldLabel);
        visualizer.centerPosition = EditorGUILayout.Vector2IntField("Center Position", visualizer.centerPosition);
        visualizer.visualizationSize = EditorGUILayout.IntField("Visualization Size", visualizer.visualizationSize);
        visualizer.heightMultiplier = EditorGUILayout.FloatField("Height Multiplier", visualizer.heightMultiplier);
        visualizer.showGizmos = EditorGUILayout.Toggle("Show Gizmos", visualizer.showGizmos);
        visualizer.heightmapColor = EditorGUILayout.ColorField("Heightmap Color", visualizer.heightmapColor);

        autoUpdate = EditorGUILayout.Toggle("Auto Update", autoUpdate);

        if (EditorGUI.EndChangeCheck() || GUILayout.Button("Update View"))
        {
            SceneView.RepaintAll();
        }
    }
} 