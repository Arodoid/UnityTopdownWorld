using UnityEngine;

public class YLevelDebugGizmos : MonoBehaviour
{
    [System.Serializable]
    public struct YLevelMarker
    {
        public string label;
        public float yLevel;
        public Color color;
        public bool enabled;
    }

    [Header("Visualization Settings")]
    public float planeSize = 50f;
    public float dotSpacing = 2f;
    public bool showLabels = true;
    public float labelOffset = 2f;

    [Header("Y-Levels")]
    public YLevelMarker[] yLevels = new YLevelMarker[]
    {
        new YLevelMarker { label = "Deep Start", yLevel = 0f, color = Color.red, enabled = true },
        new YLevelMarker { label = "Cave Start", yLevel = 40f, color = Color.yellow, enabled = true },
        new YLevelMarker { label = "Cave End", yLevel = 100f, color = Color.green, enabled = true },
        new YLevelMarker { label = "Surface Start", yLevel = 70f, color = Color.magenta, enabled = true },
        new YLevelMarker { label = "Surface End", yLevel = 140f, color = Color.blue, enabled = true }
    };

    private void OnDrawGizmos()
    {
        foreach (var level in yLevels)
        {
            if (!level.enabled) continue;

            Gizmos.color = level.color;
            Vector3 center = transform.position + Vector3.up * level.yLevel;

            // Draw dotted plane
            for (float x = -planeSize; x <= planeSize; x += dotSpacing)
            {
                for (float z = -planeSize; z <= planeSize; z += dotSpacing)
                {
                    Vector3 point = center + new Vector3(x, 0, z);
                    Gizmos.DrawSphere(point, 0.1f);
                }
            }

            // Draw label
            if (showLabels)
            {
#if UNITY_EDITOR
                UnityEditor.Handles.color = level.color;
                Vector3 labelPos = center + Vector3.right * labelOffset;
                UnityEditor.Handles.Label(labelPos, $"{level.label} (Y: {level.yLevel})");
#endif
            }
        }
    }
} 