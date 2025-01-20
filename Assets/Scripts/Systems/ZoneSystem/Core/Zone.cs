using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;
using ZoneSystem.Data;

namespace ZoneSystem.Core
{
    public class Zone : MonoBehaviour
    {
        [Header("Visual Settings")]
        [SerializeField] private float heightOffset = 0.01f;
        private Material _zoneMaterial;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        
        public ZoneType Type { get; private set; }
        private HashSet<int3> _positions = new HashSet<int3>();
        private static readonly Dictionary<ZoneType, Color> ZoneColors = new()
        {
            { ZoneType.Home, new Color(0.2f, 0.6f, 1f, 0.2f) },
            { ZoneType.Area, new Color(0.8f, 0.8f, 0.8f, 0.2f) },
            { ZoneType.Stockpile, new Color(0.8f, 0.6f, 0.2f, 0.2f)},
            { ZoneType.Growing, new Color(0.2f, 0.8f, 0.2f, 0.2f) }
        };

        private void Awake() => SetupMeshComponents();

        private void SetupMeshComponents()
        {
            _meshFilter = gameObject.AddComponent<MeshFilter>();
            _meshRenderer = gameObject.AddComponent<MeshRenderer>();
            
            _zoneMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
            {
                color = Color.white
            };
            
            _zoneMaterial.SetFloat("_Surface", 1);
            _zoneMaterial.SetFloat("_Blend", 0);
            _zoneMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _zoneMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _zoneMaterial.SetInt("_ZWrite", 0);
            _zoneMaterial.DisableKeyword("_ALPHATEST_ON");
            _zoneMaterial.EnableKeyword("_ALPHABLEND_ON");
            _zoneMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            _zoneMaterial.renderQueue = 3000;
            
            _meshRenderer.material = _zoneMaterial;
        }

        public void Initialize(ZoneType type)
        {
            Type = type;
            UpdateZoneColor();
        }

        private void UpdateZoneColor()
        {
            if (_zoneMaterial != null && ZoneColors.TryGetValue(Type, out Color color))
            {
                _zoneMaterial.color = color;
            }
        }

        public void AddPositions(IEnumerable<int3> newPositions)
        {
            bool changed = false;
            foreach (var pos in newPositions)
            {
                if (_positions.Add(pos)) changed = true;
            }
            
            if (changed) UpdateVisuals();
        }

        public void RemovePositions(IEnumerable<int3> positionsToRemove)
        {
            bool changed = false;
            foreach (var pos in positionsToRemove)
            {
                if (_positions.Remove(pos)) changed = true;
            }
            
            if (changed) UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            if (_positions.Count == 0)
            {
                _meshFilter.mesh = null;
                return;
            }

            // Find bounds
            int minX = int.MaxValue, minZ = int.MaxValue;
            int maxX = int.MinValue, maxZ = int.MinValue;
            int y = 0;

            foreach (var pos in _positions)
            {
                minX = Mathf.Min(minX, pos.x);
                maxX = Mathf.Max(maxX, pos.x);
                minZ = Mathf.Min(minZ, pos.z);
                maxZ = Mathf.Max(maxZ, pos.z);
                y = pos.y; // Assuming all positions are at same height
            }

            // Create a grid to help with greedy meshing
            bool[,] grid = new bool[maxX - minX + 2, maxZ - minZ + 2];
            foreach (var pos in _positions)
            {
                grid[pos.x - minX, pos.z - minZ] = true;
            }

            var mesh = new Mesh();
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var uvs = new List<Vector2>();

            // Greedy mesh generation
            for (int x = 0; x < grid.GetLength(0); x++)
            {
                for (int z = 0; z < grid.GetLength(1); z++)
                {
                    if (!grid[x, z]) continue;

                    // Find largest possible rectangle at this position
                    int width = 1, height = 1;
                    while (x + width < grid.GetLength(0) && CanExpandWidth(grid, x, z, width, height)) width++;
                    while (z + height < grid.GetLength(1) && CanExpandHeight(grid, x, z, width, height)) height++;

                    // Mark used positions
                    for (int dx = 0; dx < width; dx++)
                        for (int dz = 0; dz < height; dz++)
                            grid[x + dx, z + dz] = false;

                    // Add rectangle to mesh
                    AddRectangle(vertices, triangles, uvs, 
                        new Vector3(x + minX, y + heightOffset, z + minZ),
                        width, height);
                }
            }

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles.ToArray(), 0);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateNormals();
            
            _meshFilter.mesh = mesh;
        }

        private bool CanExpandWidth(bool[,] grid, int x, int z, int currentWidth, int currentHeight)
        {
            if (x + currentWidth >= grid.GetLength(0)) return false;
            for (int dz = 0; dz < currentHeight; dz++)
                if (!grid[x + currentWidth, z + dz]) return false;
            return true;
        }

        private bool CanExpandHeight(bool[,] grid, int x, int z, int currentWidth, int currentHeight)
        {
            if (z + currentHeight >= grid.GetLength(1)) return false;
            for (int dx = 0; dx < currentWidth; dx++)
                if (!grid[x + dx, z + currentHeight]) return false;
            return true;
        }

        private void AddRectangle(List<Vector3> vertices, List<int> triangles, List<Vector2> uvs, 
            Vector3 position, float width, float height)
        {
            int vertexIndex = vertices.Count;

            vertices.Add(position);
            vertices.Add(position + new Vector3(width, 0, 0));
            vertices.Add(position + new Vector3(width, 0, height));
            vertices.Add(position + new Vector3(0, 0, height));

            triangles.Add(vertexIndex);
            triangles.Add(vertexIndex + 2);
            triangles.Add(vertexIndex + 1);
            triangles.Add(vertexIndex);
            triangles.Add(vertexIndex + 3);
            triangles.Add(vertexIndex + 2);

            uvs.Add(new Vector2(0, 0));
            uvs.Add(new Vector2(width, 0));
            uvs.Add(new Vector2(width, height));
            uvs.Add(new Vector2(0, height));
        }

        public bool ContainsPosition(int3 position) => _positions.Contains(position);
        public bool IsEmpty => _positions.Count == 0;

        private void OnDestroy()
        {
            if (_zoneMaterial != null) Destroy(_zoneMaterial);
        }
    }
} 