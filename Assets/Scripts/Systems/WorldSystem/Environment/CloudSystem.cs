using UnityEngine;

namespace WorldSystem.Environment
{
    [ExecuteInEditMode]
    public class CloudSystem : MonoBehaviour
    {
        [SerializeField] private Material targetMaterial;

        [Header("Cloud Settings")]
        [SerializeField] private float cloudScale = 25f;
        [SerializeField] private float cloudSpeed = 0.05f;
        [SerializeField] private float cloudDensity = 0.3f;
        [SerializeField] private float cloudHeight = 100f;
        [SerializeField] private float cloudMaxOrthoSize = 100f;
        [SerializeField] private float cloudMinOrthoSize = 20f;
        [SerializeField] private Color cloudColor = new Color(1, 1, 1, 0.6f);
        [SerializeField] private float cloudShadowStrength = 0.3f;

        [Header("Shadow Settings")]
        [SerializeField] private float shadowSoftness = 0.3f;
        [SerializeField] private float overcastFactor = 0f;
        [SerializeField] private float shadowMaxOrthoSize = 100f;
        [SerializeField] private float shadowMinOrthoSize = 20f;

        private void OnValidate()
        {
            UpdateMaterialProperties();
        }

        private void Update()
        {
            UpdateMaterialProperties();
        }

        private void UpdateMaterialProperties()
        {
            if (targetMaterial == null) return;

            targetMaterial.SetFloat("_CloudScale", cloudScale);
            targetMaterial.SetFloat("_CloudSpeed", cloudSpeed);
            targetMaterial.SetFloat("_CloudDensity", cloudDensity);
            targetMaterial.SetFloat("_CloudHeight", cloudHeight);
            targetMaterial.SetColor("_CloudColor", cloudColor);
            targetMaterial.SetFloat("_ShadowStrength", cloudShadowStrength);
            targetMaterial.SetFloat("_CloudMaxOrthoSize", cloudMaxOrthoSize);
            targetMaterial.SetFloat("_CloudMinOrthoSize", cloudMinOrthoSize);
            
            // Shadow properties
            targetMaterial.SetFloat("_ShadowSoftness", shadowSoftness);
            targetMaterial.SetFloat("_OvercastFactor", overcastFactor);
            targetMaterial.SetFloat("_ShadowMaxOrthoSize", shadowMaxOrthoSize);
            targetMaterial.SetFloat("_ShadowMinOrthoSize", shadowMinOrthoSize);

            // Calculate shadow offset based on main directional light
            Light mainLight = RenderSettings.sun;
            if (mainLight != null)
            {
                Vector3 lightDir = -mainLight.transform.forward.normalized;
                targetMaterial.SetVector("_MainLightPosition", new Vector4(lightDir.x, lightDir.y, lightDir.z, 0));
                
                Vector2 shadowOffset = new Vector2(lightDir.x, lightDir.z) * (cloudHeight / -lightDir.y);
                targetMaterial.SetVector("_ShadowOffset", shadowOffset);
            }
        }
    }
} 