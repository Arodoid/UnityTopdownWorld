using UnityEngine;

namespace EntitySystem.Core.Components
{
    public class EntityVisualComponent : EntityComponent
    {
        [Header("Visual Settings")]
        [SerializeField] private Color _color = Color.white;
        [SerializeField] private float _radius = 0.4f;

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Material _material;

        protected override void OnInitialize(EntityManager entityManager)
        {
            SetupVisuals();
        }

        private void SetupVisuals()
        {            
            var visualObject = new GameObject("Visual");
            visualObject.transform.SetParent(transform);
            visualObject.transform.localPosition = Vector3.zero;
            
            // Add components to the child object
            _meshFilter = visualObject.AddComponent<MeshFilter>();
            _meshRenderer = visualObject.AddComponent<MeshRenderer>();

            // Create material
            _material = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
            {
                color = _color
            };
            _meshRenderer.material = _material;

            var mesh = CreateBoxMesh();
            _meshFilter.mesh = mesh;
        }

        private Mesh CreateBoxMesh()
        {
            var mesh = new Mesh();
            float size = _radius * 2;

            // Define vertices with bottom at y=0
            Vector3[] vertices = new Vector3[]
            {
                // Bottom vertices at y=0
                new Vector3(-size/2, 0, -size/2),
                new Vector3(size/2, 0, -size/2),
                new Vector3(size/2, size, -size/2),
                new Vector3(-size/2, size, -size/2),
                new Vector3(-size/2, 0, size/2),
                new Vector3(size/2, 0, size/2),
                new Vector3(size/2, size, size/2),
                new Vector3(-size/2, size, size/2)
            };

            // Fixed triangle indices to face outward
            int[] triangles = new int[]
            {
                // Front face
                4, 5, 6,
                4, 6, 7,
                // Back face
                1, 0, 2,
                0, 3, 2,
                // Left face
                0, 4, 7,
                0, 7, 3,
                // Right face
                5, 1, 2,
                5, 2, 6,
                // Top face
                3, 7, 6,
                3, 6, 2,
                // Bottom face
                0, 1, 5,
                0, 5, 4
            };

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