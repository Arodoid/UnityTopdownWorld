using UnityEngine;
using UnityEditor;
using Unity.Mathematics;

namespace WorldSystem.Generation
{
    public class NoiseVisualizer : MonoBehaviour
    {
        [Header("Noise Settings")]
        public float noiseScale = 0.03f;
        public int seed;
        public int octaves = 5;
        
        [Header("Visualization")]
        public Vector2Int centerPosition;
        public int visualizationSize = 64;
        public float heightMultiplier = 8f;
        public bool showGizmos = true;
        public Color heightmapColor = new Color(0.2f, 1f, 0.3f, 0.5f);

        private void OnDrawGizmos()
        {
            if (!showGizmos) return;

            Gizmos.color = heightmapColor;
            
            for (int x = 0; x < visualizationSize; x++)
            {
                for (int z = 0; z < visualizationSize; z++)
                {
                    float2 worldPos = new float2(
                        centerPosition.x + x,
                        centerPosition.y + z
                    );

                    float noiseValue = NoiseUtility.FBM(worldPos * noiseScale, seed, octaves);
                    float height = 64 + (noiseValue * heightMultiplier);

                    Vector3 pos = new Vector3(x, height, z);
                    Gizmos.DrawCube(pos, Vector3.one * 0.5f);
                }
            }
        }
    }
} 