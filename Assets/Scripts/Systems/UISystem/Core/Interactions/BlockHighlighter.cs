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
        [SerializeField] private float heightOffset = 0.001f; // Tiny offset to prevent z-fighting

        private WorldCoordinateMapper _coordinateMapper;
        private WorldSystemAPI _worldAPI;
        private GameObject _highlightPlane;
        private MeshRenderer _highlightRenderer;
        private int3? _currentHighlightedPosition;
        
        // Only need vertices for the top face
        private static readonly Vector3[] VERTICES = new Vector3[]
        {
            new Vector3(0, 0, 0), // Changed Y from 1 to 0
            new Vector3(1, 0, 0),
            new Vector3(1, 0, 1),
            new Vector3(0, 0, 1)
        };

        private static readonly int[] TRIANGLES = new int[]
        {
            // Top face only
            0, 2, 1,
            0, 3, 2
        };

        private static readonly Vector2[] UVS = new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(1, 1),
            new Vector2(0, 1)
        };

        private void Awake()
        {
            if (highlightMaterial == null)
            {
                // Create URP transparent material
                highlightMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                {
                    color = new Color(validHighlightColor.r, validHighlightColor.g, validHighlightColor.b, 0.3f)
                };
                
                // Setup material for transparency in URP
                highlightMaterial.SetFloat("_Surface", 1); // 0 = opaque, 1 = transparent
                highlightMaterial.SetFloat("_Blend", 0);   // 0 = alpha blend
                highlightMaterial.SetFloat("_Metallic", 0.0f);
                highlightMaterial.SetFloat("_Smoothness", 0.5f);
                highlightMaterial.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                highlightMaterial.renderQueue = 3000;
                highlightMaterial.SetShaderPassEnabled("ShadowCaster", false);
            }
        }

        public void Initialize(WorldCoordinateMapper coordinateMapper, WorldSystemAPI worldAPI)
        {
            _coordinateMapper = coordinateMapper;
            _worldAPI = worldAPI;
            CreateHighlightMesh();
        }

        private void CreateHighlightMesh()
        {
            _highlightPlane = new GameObject("BlockHighlight");
            _highlightPlane.transform.SetParent(transform);

            var mesh = new Mesh();
            mesh.vertices = VERTICES;
            mesh.triangles = TRIANGLES;
            mesh.uv = UVS;
            mesh.RecalculateNormals();

            var meshFilter = _highlightPlane.AddComponent<MeshFilter>();
            meshFilter.mesh = mesh;

            _highlightRenderer = _highlightPlane.AddComponent<MeshRenderer>();
            _highlightRenderer.material = highlightMaterial;
            
            _highlightPlane.SetActive(false);
        }

        public void UpdateHighlight(Vector2 mousePosition)
        {
            int maxYLevel = _worldAPI.GetCurrentViewLevel();
            
            if (_coordinateMapper.TryGetHoveredBlockPosition(mousePosition, out int3 blockPos))
            {
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

        public void ShowHighlight()
        {
            if (!_highlightPlane.activeSelf)
                _highlightPlane.SetActive(true);
        }

        private void ShowHighlight(int3 position)
        {
            ShowHighlight();
            // Position the highlight plane
            Vector3 highlightPos = new Vector3(position.x, position.y + heightOffset, position.z);
            _highlightPlane.transform.position = highlightPos;
        }

        public void HideHighlight()
        {
            if (_highlightPlane.activeSelf)
                _highlightPlane.SetActive(false);
            _currentHighlightedPosition = null;
        }

        public int3? GetHighlightedPosition() => _currentHighlightedPosition;

        private void OnDestroy()
        {
            if (_highlightPlane != null)
                Destroy(_highlightPlane);
        }
    }
}