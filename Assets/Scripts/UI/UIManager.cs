using UnityEngine;
using EntitySystem.Core;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }
    
    [SerializeField] private EntityInfoPanel _entityPanel;
    private Entity _selectedEntity;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Verify references
        if (_entityPanel == null)
        {
            _entityPanel = GetComponentInChildren<EntityInfoPanel>();
            if (_entityPanel == null)
            {
                Debug.LogError("UIManager: Missing EntityInfoPanel reference!");
            }
        }
    }

    public void ShowEntityPanel(Entity entity)
    {
        if (_entityPanel == null)
        {
            Debug.LogError("UIManager: No EntityInfoPanel assigned!");
            return;
        }

        _selectedEntity = entity;
        _entityPanel.Show(entity);
    }

    void Update()
    {
        // Keep updating panel if entity is selected
        if (_selectedEntity != null && _entityPanel != null)
        {
            _entityPanel.Show(_selectedEntity);
        }
    }

    public void Setup(EntityInfoPanel panel)
    {
        _entityPanel = panel;
    }
} 