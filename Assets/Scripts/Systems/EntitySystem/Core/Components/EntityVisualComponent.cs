using UnityEngine;

namespace EntitySystem.Core.Components
{
    public class EntityVisualComponent : EntityComponent
    {
        [Header("Visual Settings")]
        [SerializeField] private Color _color = Color.white;
        [SerializeField] private float _radius = 0.4f;
        [SerializeField] private float _heightOffset = 0.01f;

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Material _material;

        protected override void OnInitialize(EntityManager entityManager)
        {
            SetupVisuals();
        }

        private void SetupVisuals()
        {
            // Create a child GameObject for the visual
            var visualObject = new GameObject("Visual");
            visualObject.transform.SetParent(transform);
            visualObject.transform.localPosition = new Vector3(0, _heightOffset, 0);
            
            // Add components to the child object
            _meshFilter = visualObject.AddComponent<MeshFilter>();
            _meshRenderer = visualObject.AddComponent<MeshRenderer>();

            // Create material
            _material = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
            {
                color = _color
            };
            _meshRenderer.material = _material;

            // Create circle mesh
            var mesh = CreateCircleMesh();
            _meshFilter.mesh = mesh;
        }

        private Mesh CreateCircleMesh()
        {
            var mesh = new Mesh();
            int segments = 16;
            var vertices = new Vector3[segments + 1];
            var triangles = new int[segments * 3];

            // Center vertex
            vertices[0] = Vector3.zero;

            // Create circle vertices
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                vertices[i + 1] = new Vector3(
                    Mathf.Cos(angle) * _radius,
                    0,
                    Mathf.Sin(angle) * _radius
                );
            }

            // Create triangles (reversed winding order)
            for (int i = 0; i < segments; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 2 > segments ? 1 : i + 2;
                triangles[i * 3 + 2] = i + 1;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();

            return mesh;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();  // Call base class OnDestroy first
            if (_material != null)
            {
                Destroy(_material);
            }
        }

        public void SetColor(Color color)
        {
            _color = color;
            if (_material != null)
            {
                _material.color = color;
            }
        }
    }
}