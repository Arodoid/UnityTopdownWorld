using UnityEngine;
using Unity.Mathematics;

namespace UISystem.Core.Interactions
{
    public class BoxVisualizer : MonoBehaviour
    {
        private LineRenderer _lineRenderer;
        private static readonly Vector3[] _corners = new Vector3[8];
        private static readonly int[] _lineIndices = new int[]
        {
            0, 1, 1, 2, 2, 3, 3, 0, // Bottom face
            4, 5, 5, 6, 6, 7, 7, 4, // Top face
            0, 4, 1, 5, 2, 6, 3, 7  // Vertical edges
        };

        private void Awake()
        {
            _lineRenderer = gameObject.AddComponent<LineRenderer>();
            _lineRenderer.material = new Material(Shader.Find("Unlit/Color"));
            _lineRenderer.material.color = Color.white;
            _lineRenderer.startWidth = 0.1f;
            _lineRenderer.endWidth = 0.1f;
            _lineRenderer.positionCount = _lineIndices.Length;
            Hide();
        }

        public void Show(int3 start, int3 end)
        {
            var (min, max) = GetBoxBounds(start, end);
            
            // Set corners - only add +1 to X and Z to enclose blocks horizontally
            _corners[0] = new Vector3(min.x, min.y, min.z);
            _corners[1] = new Vector3(max.x + 1, min.y, min.z);
            _corners[2] = new Vector3(max.x + 1, min.y, max.z + 1);
            _corners[3] = new Vector3(min.x, min.y, max.z + 1);
            
            // Top face - no +1 to Y anymore
            _corners[4] = new Vector3(min.x, max.y, min.z);
            _corners[5] = new Vector3(max.x + 1, max.y, min.z);
            _corners[6] = new Vector3(max.x + 1, max.y, max.z + 1);
            _corners[7] = new Vector3(min.x, max.y, max.z + 1);

            // Update line renderer
            for (int i = 0; i < _lineIndices.Length; i++)
            {
                _lineRenderer.SetPosition(i, _corners[_lineIndices[i]]);
            }
            
            _lineRenderer.enabled = true;
        }

        public void Hide()
        {
            _lineRenderer.enabled = false;
        }

        private (int3 min, int3 max) GetBoxBounds(int3 start, int3 end)
        {
            return (
                new int3(
                    Mathf.Min(start.x, end.x),
                    Mathf.Min(start.y, end.y),
                    Mathf.Min(start.z, end.z)
                ),
                new int3(
                    Mathf.Max(start.x, end.x),
                    Mathf.Max(start.y, end.y),
                    Mathf.Max(start.z, end.z)
                )
            );
        }
    }
} 