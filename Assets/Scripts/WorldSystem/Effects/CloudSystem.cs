using UnityEngine;

namespace WorldSystem.Effects
{
    [RequireComponent(typeof(Camera))]
    public class CloudSystem : MonoBehaviour
    {
        [Header("Cloud Settings")]
        [SerializeField] private Material cloudMaterial;
        [SerializeField] private float cloudScale = 20f;
        [SerializeField] private float cloudSpeed = 0.05f;
        [SerializeField] private float cloudDensity = 0.3f;
        [SerializeField] private float cloudHeight = 100f;
        [SerializeField] private Color cloudColor = new Color(1, 1, 1, 0.6f);
        [SerializeField] private float shadowStrength = 0.3f;
        
        [Header("Debug")]
        [SerializeField] private bool showDebug = true;
        
        private Camera _camera;
        
        private void Start()
        {
            _camera = GetComponent<Camera>();
            
            if (!_camera.orthographic)
            {
                Debug.LogWarning("Camera is not orthographic! Setting to orthographic mode.");
                _camera.orthographic = true;
            }
            
            if (cloudMaterial == null)
            {
                Debug.LogError("Cloud material is not assigned!");
                enabled = false;
                return;
            }
            
            // Create a copy of the material to avoid modifying the asset
            cloudMaterial = new Material(cloudMaterial);
            
            // Adjust initial cloud scale based on camera settings
            cloudScale = Mathf.Max(20f, _camera.orthographicSize * 2f);
            
            UpdateCloudProperties();
            Debug.Log($"CloudSystem initialized with: Camera Height={_camera.transform.position.y}, OrthoSize={_camera.orthographicSize}, CloudScale={cloudScale}");
        }
        
        private void OnGUI()
        {
            if (!showDebug) return;
            
            GUILayout.BeginArea(new Rect(10, 10, 300, 400));
            GUILayout.Label("Cloud Debug Controls", GUI.skin.box);
            
            cloudColor.a = GUILayout.HorizontalSlider(cloudColor.a, 0f, 1f);
            GUILayout.Label($"Cloud Opacity: {cloudColor.a:F2}");
            
            cloudScale = GUILayout.HorizontalSlider(cloudScale, 1f, 200f);
            GUILayout.Label($"Cloud Scale: {cloudScale:F1}");
            
            cloudSpeed = GUILayout.HorizontalSlider(cloudSpeed, 0f, 0.5f);
            GUILayout.Label($"Cloud Speed: {cloudSpeed:F2}");
            
            cloudDensity = GUILayout.HorizontalSlider(cloudDensity, 0f, 1f);
            GUILayout.Label($"Cloud Density: {cloudDensity:F2}");
            
            cloudHeight = GUILayout.HorizontalSlider(cloudHeight, 10f, 500f);
            GUILayout.Label($"Cloud Height: {cloudHeight:F0}");
            
            GUILayout.Label($"Camera Height: {_camera.transform.position.y:F1}");
            GUILayout.Label($"Ortho Size: {_camera.orthographicSize:F1}");
            
            if (GUILayout.Button("Reset to Defaults"))
            {
                cloudScale = Mathf.Max(20f, _camera.orthographicSize * 2f);
                cloudSpeed = 0.05f;
                cloudDensity = 0.3f;
                cloudHeight = 100f;
                cloudColor = new Color(1, 1, 1, 0.6f);
                UpdateCloudProperties();
            }
            
            GUILayout.EndArea();
        }
        
        private void Update()
        {
            // Update cloud properties based on camera
            cloudMaterial.SetFloat("_OrthoSize", _camera.orthographicSize);
            UpdateCloudProperties();
        }
        
        private void UpdateCloudProperties()
        {
            cloudMaterial.SetFloat("_CloudScale", cloudScale);
            cloudMaterial.SetFloat("_CloudSpeed", cloudSpeed);
            cloudMaterial.SetFloat("_CloudDensity", cloudDensity);
            cloudMaterial.SetFloat("_CloudHeight", cloudHeight);
            cloudMaterial.SetColor("_CloudColor", cloudColor);
            cloudMaterial.SetFloat("_ShadowStrength", shadowStrength);
        }
        
        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (cloudMaterial == null)
            {
                Graphics.Blit(source, destination);
                return;
            }
            Graphics.Blit(source, destination, cloudMaterial);
        }
        
        private void OnDestroy()
        {
            if (cloudMaterial != null)
            {
                Destroy(cloudMaterial);
            }
        }
    }
} 