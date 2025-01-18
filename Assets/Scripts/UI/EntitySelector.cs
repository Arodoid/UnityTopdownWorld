using UnityEngine;
using EntitySystem.Core;
using System.Linq;

public class EntitySelector : MonoBehaviour
{
    private Camera _mainCamera;
    private EntityManager _entityManager;
    
    void Start()
    {
        _mainCamera = Camera.main;
        _entityManager = Object.FindFirstObjectByType<EntityManager>();
        
        if (_mainCamera == null)
            Debug.LogError("EntitySelector: No main camera found!");
        if (_entityManager == null)
            Debug.LogError("EntitySelector: No EntityManager found!");
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0)) // Left click
        {
            Vector3 mousePos = Input.mousePosition;
            Vector3 worldPos = _mainCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, _mainCamera.transform.position.y));
            
            // Convert to grid coordinates
            int gridX = Mathf.RoundToInt(worldPos.x);
            int gridZ = Mathf.RoundToInt(worldPos.z);

            Debug.Log($"Trying to select at grid position: ({gridX}, {gridZ})");

            // Get entities from EntityManager that are in this cell
            var entitiesInCell = _entityManager.GetEntitiesInCell(new Vector2Int(gridX, gridZ));
            var selectedEntity = entitiesInCell.FirstOrDefault() as Entity; // Get first entity or null
            
            if (selectedEntity != null)
            {
                Debug.Log($"Found entity at grid ({gridX}, {gridZ})");
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.ShowEntityPanel(selectedEntity);
                }
            }
            else
            {
                Debug.Log($"No entity found at grid ({gridX}, {gridZ})");
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.ShowEntityPanel(null);
                }
            }
        }
    }
} 