using UnityEngine;
using TMPro;
using EntitySystem.Core;
using JobSystem.Core;

public class EntityInfoPanel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _jobText;
    [SerializeField] private TextMeshProUGUI _statusText;
    [SerializeField] private GameObject _panel;

    private void Awake()
    {
        // Ensure panel starts hidden
        if (_panel != null)
            _panel.SetActive(false);
    }

    public void Show(Entity entity)
    {
        if (_panel == null || _nameText == null || _jobText == null || _statusText == null)
        {
            Debug.LogError("EntityInfoPanel: Missing UI references!");
            return;
        }

        if (entity == null)
        {
            Hide();
            return;
        }

        _panel.SetActive(true);
        
        // Basic info
        _nameText.text = $"Entity: {entity.Id}";
        
        // Job info
        var jobComponent = entity.GetComponent<JobComponent>();
        _jobText.text = jobComponent != null ? 
            $"Current Job: {jobComponent.CurrentJob?.GetType().Name ?? "None"}" : 
            "No Job Component";
        
        // Position info
        var position = entity.GameObject.transform.position;
        _statusText.text = $"Position: ({position.x:F1}, {position.y:F1}, {position.z:F1})";
    }

    public void Hide()
    {
        if (_panel != null)
            _panel.SetActive(false);
    }

    public void Setup(TextMeshProUGUI nameText, TextMeshProUGUI jobText, TextMeshProUGUI statusText, GameObject panel)
    {
        _nameText = nameText;
        _jobText = jobText;
        _statusText = statusText;
        _panel = panel;
    }
} 