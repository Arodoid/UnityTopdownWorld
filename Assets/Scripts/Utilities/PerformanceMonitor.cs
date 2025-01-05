using UnityEngine;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public class PerformanceMonitor : MonoBehaviour
{
    private class MetricData
    {
        public float[] samples;
        public int currentIndex;
        public float totalTime;
        public int sampleCount;
        public float maxTime;
        public float lastUpdateTime;
        public readonly int maxSamples;
        public float maxTimeExcludingWarmup;
        private bool warmupPeriodEnded;
        private const float WARMUP_TIME = 5f;

        public MetricData(int maxSamples = 800)
        {
            this.maxSamples = maxSamples;
            this.samples = new float[maxSamples];
            this.currentIndex = 0;
            this.warmupPeriodEnded = false;
        }

        public void AddSample(float time)
        {
            if (!warmupPeriodEnded && Time.realtimeSinceStartup > WARMUP_TIME)
            {
                warmupPeriodEnded = true;
                Reset();
                maxTimeExcludingWarmup = 0f;
                return;
            }

            totalTime -= samples[currentIndex];
            totalTime += time;
            
            if (sampleCount >= maxSamples)
            {
                maxTime = 0f;
                for (int i = 0; i < samples.Length; i++)
                {
                    maxTime = Mathf.Max(maxTime, samples[i]);
                }
            }
            maxTime = Mathf.Max(maxTime, time);
            
            if (warmupPeriodEnded)
            {
                maxTimeExcludingWarmup = Mathf.Max(maxTimeExcludingWarmup, time);
            }
            
            samples[currentIndex] = time;
            currentIndex = (currentIndex + 1) % maxSamples;
            
            sampleCount = Mathf.Min(sampleCount + 1, maxSamples);
            
            lastUpdateTime = Time.realtimeSinceStartup;
        }

        public void Reset()
        {
            System.Array.Clear(samples, 0, samples.Length);
            currentIndex = 0;
            totalTime = 0;
            sampleCount = 0;
            maxTime = 0;
            lastUpdateTime = Time.realtimeSinceStartup;
        }

        public float GetMean()
        {
            if (sampleCount == 0) return 0;
            return totalTime / sampleCount;
        }

        public void Update()
        {
            if (Time.realtimeSinceStartup - lastUpdateTime > 1f/30f)
            {
                AddSample(0);
                sampleCount = Mathf.Max(0, sampleCount - 1);
                lastUpdateTime = Time.realtimeSinceStartup;
            }
        }

        public float[] GetTimelineSamples()
        {
            float[] result = new float[maxSamples];
            for (int i = 0; i < maxSamples; i++)
            {
                int index = (currentIndex - maxSamples + i + maxSamples) % maxSamples;
                result[i] = samples[index];
            }
            return result;
        }

        public float GetMaxExcludingWarmup()
        {
            return maxTimeExcludingWarmup;
        }
    }

    private static readonly Dictionary<string, MetricData> metrics = new();
    private static readonly Dictionary<string, Stopwatch> activeStopwatches = new();
    
    private static readonly Color[] graphColors = new[]
    {
        new Color(1, 0.3f, 0.3f),      // Light Red
        new Color(0.3f, 1, 0.3f),      // Light Green
        new Color(0.3f, 0.3f, 1),      // Light Blue
        new Color(1, 1, 0.3f),         // Light Yellow
        new Color(0.3f, 1, 1),         // Light Cyan
        new Color(1, 0.3f, 1)          // Light Magenta
    };

    private GUIStyle titleStyle;
    private GUIStyle labelStyle;
    private Texture2D backgroundTexture;
    private bool stylesInitialized = false;

    private float testTimer = 0f;
    private bool isTestRunning = false;

    private Vector2 scrollPosition = Vector2.zero;

    private void InitializeStyles()
    {
        if (stylesInitialized) return;

        backgroundTexture = new Texture2D(1, 1);
        backgroundTexture.SetPixel(0, 0, new Color(0.1f, 0.1f, 0.1f, 0.95f));
        backgroundTexture.Apply();

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            normal = { textColor = new Color(0.9f, 0.9f, 0.9f) }
        };

        stylesInitialized = true;
    }

    private void OnGUI()
    {
        if (!UnityEngine.Debug.isDebugBuild)
        {
            return;
        }

        InitializeStyles();
        if (!stylesInitialized) return;

        float padding = 10;
        float startY = 10;
        float startX = 10;
        float width = 800;
        float headerHeight = 30;
        float metricHeight = 80;
        float graphWidth = 400;
        float graphHeight = 60;
        
        float contentHeight = headerHeight + (metrics.Count * metricHeight) + (padding * 2);
        
        float maxVisibleHeight = Screen.height * 0.8f;
        float actualHeight = Mathf.Min(contentHeight, maxVisibleHeight);

        GUI.DrawTexture(new Rect(startX, startY, width + 20, actualHeight), backgroundTexture);

        GUI.Label(new Rect(startX + padding, startY + padding, width - padding * 2, headerHeight), 
            "PERFORMANCE MONITOR", titleStyle);

        if (GUI.Button(new Rect(width - 100, startY + padding, 80, 20), "Clear"))
        {
            Clear();
        }

        scrollPosition = GUI.BeginScrollView(
            new Rect(startX, startY + headerHeight, width + 20, actualHeight - headerHeight),
            scrollPosition,
            new Rect(0, 0, width - 20, contentHeight - headerHeight)
        );

        float y = padding;
        int index = 0;

        foreach (var metric in metrics.OrderBy(m => m.Key))
        {
            string name = metric.Key;
            MetricData data = metric.Value;
            Color graphColor = graphColors[index % graphColors.Length];

            GUI.DrawTexture(
                new Rect(padding, y, width - padding * 2 - 20, metricHeight - padding),
                backgroundTexture);

            GUI.Label(
                new Rect(padding * 2, y, width - padding * 2, 20),
                $"{name}:", labelStyle);
            
            GUI.Label(
                new Rect(padding * 2 + graphWidth + 10, y, 300, 20),
                $"Avg: {data.GetMean():F3}ms | Max: {data.maxTime:F3}ms | MaxNoWarmup: {data.GetMaxExcludingWarmup():F3}ms", 
                labelStyle);

            DrawGraph(data, 
                padding * 2,
                y + labelStyle.fontSize + 5,
                graphWidth,
                graphHeight,
                graphColor);

            y += metricHeight;
            index++;
        }

        GUI.EndScrollView();
    }

    private void DrawGraph(MetricData data, float x, float y, float width, float height, Color color)
    {
        GUI.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
        GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        for (int i = 1; i < 4; i++)
        {
            float lineY = y + (height * i / 4);
            GUI.DrawTexture(new Rect(x, lineY, width, 1), Texture2D.whiteTexture);
        }
        GUI.color = Color.white;

        float[] samples = data.GetTimelineSamples();
        float maxValue = data.maxTime > 0 ? data.maxTime : 1f;
        
        Color graphColor = new Color(color.r, color.g, color.b, 0.8f);
        for (int i = 0; i < samples.Length - 1; i++)
        {
            float x1 = x + (i * width / data.maxSamples);
            float x2 = x + ((i + 1) * width / data.maxSamples);
            float y1 = y + height - (samples[i] / maxValue * height);
            float y2 = y + height - (samples[i + 1] / maxValue * height);
            
            DrawLine(new Vector2(x1, y1), new Vector2(x2, y2), graphColor);
        }
    }

    private void DrawLine(Vector2 start, Vector2 end, Color color)
    {
        float thickness = 2f;
        Vector2 direction = (end - start).normalized;
        float length = Vector2.Distance(start, end);

        GUI.color = color;
        GUIUtility.RotateAroundPivot(
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg,
            start);
        
        GUI.DrawTexture(
            new Rect(start.x, start.y - thickness/2, length, thickness),
            Texture2D.whiteTexture);
        
        GUI.matrix = Matrix4x4.identity;
        GUI.color = Color.white;
    }

    public static void StartMeasurement(string name)
    {
        var sw = new Stopwatch();
        sw.Start();
        activeStopwatches[name] = sw;
    }

    public static void EndMeasurement(string name)
    {
        if (activeStopwatches.TryGetValue(name, out Stopwatch sw))
        {
            sw.Stop();
            float ms = sw.ElapsedTicks / (float)System.TimeSpan.TicksPerMillisecond;
            
            if (!metrics.TryGetValue(name, out MetricData data))
            {
                data = new MetricData();
                metrics[name] = data;
            }
            
            data.AddSample(ms);
            data.lastUpdateTime = Time.realtimeSinceStartup;
            activeStopwatches.Remove(name);
        }
    }

    public static void Clear()
    {
        metrics.Clear();
        activeStopwatches.Clear();
    }

    private void OnDestroy()
    {
        if (backgroundTexture != null)
        {
            Destroy(backgroundTexture);
        }
    }

    private void Update()
    {
        foreach (var metric in metrics.Values)
        {
            metric.Update();
        }
    }

    private void Start()
    {
        UnityEngine.Debug.Log("PerformanceMonitor started");
    }

    private void OnEnable()
    {
        Clear();
    }
} 