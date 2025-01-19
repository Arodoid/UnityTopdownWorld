using UnityEngine;
using Unity.Mathematics;
using WorldSystem.API;
using WorldSystem.Data;

namespace UISystem.Core.Interactions
{
    public class BlockHighlighter : MonoBehaviour
    {
        [Header("Visual Settings")]
        [SerializeField] private Material highlightMaterial;
        [SerializeField] private Color validHighlightColor = Color.green;
        [SerializeField] private Color invalidHighlightColor = Color.red;
        [SerializeField] private float highlightScale = 1.02f;

        private WorldCoordinateMapper _coordinateMapper;
        private WorldSystemAPI _worldAPI;
        private GameObject _highlightCube;
        private MeshRenderer _highlightRenderer;
        private int3? _currentHighlightedPosition;
        
        private static readonly Vector3[] VERTICES = new Vector3[]
        {
            new Vector3(0, 0, 0),
            new Vector3(1, 0, 0),
            new Vector3(1, 1, 0),
            new Vector3(0, 1, 0),
            new Vector3(0, 0, 1),
            new Vector3(1, 0, 1),
            new Vector3(1, 1, 1),
            new Vector3(0, 1, 1)
        };

        private static readonly int[] TRIANGLES = new int[]
        {
            // Front
            0, 2, 1, 0, 3, 2,
            // Right
            1, 2, 6, 1, 6, 5,
            // Back
            5, 6, 7, 5, 7, 4,
            // Left
            4, 7, 3, 4, 3, 0,
            // Top
            3, 7, 6, 3, 6, 2,
            // Bottom
            4, 0, 1, 4, 1, 5
        };

        public void Initialize(WorldCoordinateMapper coordinateMapper, WorldSystemAPI worldAPI)
        {
            _coordinateMapper = coordinateMapper;
            _worldAPI = worldAPI;
            CreateHighlightMesh();
        }

        private void CreateHighlightMesh()
        {
            _highlightCube = new GameObject("BlockHighlight");
            _highlightCube.transform.SetParent(transform);

            var mesh = new Mesh();
            mesh.vertices = VERTICES;
            mesh.triangles = TRIANGLES;
            mesh.RecalculateNormals();

            var meshFilter = _highlightCube.AddComponent<MeshFilter>();
            meshFilter.mesh = mesh;

            _highlightRenderer = _highlightCube.AddComponent<MeshRenderer>();
            _highlightRenderer.material = highlightMaterial;
            
            _highlightCube.SetActive(false);
        }

        public void UpdateHighlight(Vector2 mousePosition)
        {
            int maxYLevel = _worldAPI.GetCurrentViewLevel();
            
            if (_coordinateMapper.TryGetHoveredBlockPosition(mousePosition, out int3 blockPos))
            {
                // Only highlight if block is at or below current view level
                if (blockPos.y <= maxYLevel)
                {
                    ShowHighlight(blockPos);
                    _currentHighlightedPosition = blockPos;
                    return;
                }
            }

            HideHighlight();
            _currentHighlightedPosition = null;
        }

        private void ShowHighlight(int3 position)
        {
            if (!_highlightCube.activeSelf)
                _highlightCube.SetActive(true);

            // Position the highlight cube
            Vector3 highlightPos = new Vector3(position.x, position.y, position.z);
            _highlightCube.transform.position = highlightPos;
            
            // Scale slightly larger than the block
            _highlightCube.transform.localScale = Vector3.one * highlightScale;
        }

        public void HideHighlight()
        {
            if (_highlightCube.activeSelf)
                _highlightCube.SetActive(false);
            _currentHighlightedPosition = null;
        }

        public int3? GetHighlightedPosition() => _currentHighlightedPosition;

        private void OnDestroy()
        {
            if (_highlightCube != null)
                Destroy(_highlightCube);
        }
    }
}