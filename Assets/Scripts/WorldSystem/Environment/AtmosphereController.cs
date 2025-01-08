using UnityEngine;

namespace WorldSystem.Environment
{
    [ExecuteInEditMode]
    public class AtmosphereController : MonoBehaviour
    {
        [SerializeField] private Material targetMaterial;
        [SerializeField] private Camera mainCamera;

        [Header("Atmosphere Settings")]
        [SerializeField] private Color atmosphereColor = new Color(0.6f, 0.8f, 1.0f, 1.0f);
        [SerializeField] private float atmosphereDensity = 0.1f;
        [SerializeField] private float atmosphereStartHeight = 0f;
        [SerializeField] private float atmosphereEndHeight = 256f;
        [SerializeField] private float atmosphereMaxOrthoSize = 100f;
        [SerializeField] private float atmosphereMinOrthoSize = 50f;

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

            targetMaterial.SetColor("_AtmosphereColor", atmosphereColor);
            targetMaterial.SetFloat("_AtmosphereDensity", atmosphereDensity);
            targetMaterial.SetFloat("_AtmosphereStartHeight", atmosphereStartHeight);
            targetMaterial.SetFloat("_AtmosphereEndHeight", atmosphereEndHeight);
            targetMaterial.SetFloat("_AtmosphereMaxOrthoSize", atmosphereMaxOrthoSize);
            targetMaterial.SetFloat("_AtmosphereMinOrthoSize", atmosphereMinOrthoSize);
            targetMaterial.SetFloat("_OrthoSize", mainCamera.orthographicSize);
        }
    }
} 