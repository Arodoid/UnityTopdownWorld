using UnityEngine;
using WorldSystem.Base;
using UnityEngine.UI;

public class CameraController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 50f;
    [SerializeField] private float fastMoveMultiplier = 2f;
    [SerializeField] private float movementSmoothness = 0.1f;
    
    [Header("Zoom Settings")]
    [SerializeField] private float zoomSpeed = 5f;
    [SerializeField] private float minZoom = 5f;
    [SerializeField] private float maxZoom = 100f;
    [SerializeField] private float zoomSmoothness = 0.1f;
    
    [Header("Y-Level Settings")]
    [SerializeField] private int minYLevel = 0;
    [SerializeField] private int maxYLevel = 20;
    [SerializeField] private ChunkManager chunkManager;
    [SerializeField] private Slider yLevelSlider;
    
    private Camera _camera;
    private Vector3 _targetPosition;
    private float _targetZoom;
    
    private void Start()
    {
        _camera = GetComponent<Camera>();
        
        if (!_camera.orthographic)
        {
            Debug.LogWarning("Camera is not set to orthographic mode!");
            _camera.orthographic = true;
        }

        if (chunkManager == null)
        {
            Debug.LogError("ChunkManager reference is not set in CameraController!");
        }

        InitializeYLevelSlider();

        _targetPosition = transform.position;
        _targetZoom = _camera.orthographicSize;
    }

    private void InitializeYLevelSlider()
    {
        if (yLevelSlider != null)
        {
            yLevelSlider.minValue = minYLevel;
            yLevelSlider.maxValue = maxYLevel;
            yLevelSlider.wholeNumbers = true;
            yLevelSlider.value = chunkManager != null ? chunkManager.ViewMaxYLevel : minYLevel;
            
            yLevelSlider.onValueChanged.AddListener(OnYLevelSliderChanged);
        }
        else
        {
            Debug.LogWarning("Y-Level Slider reference is not set in CameraController!");
        }
    }

    private void OnYLevelSliderChanged(float value)
    {
        if (chunkManager != null)
        {
            chunkManager.ViewMaxYLevel = (int)value;
        }
    }

    private void Update()
    {
        HandleMovement();
        HandleZoom();
    }

    private void HandleMovement()
    {
        float multiplier = Input.GetKey(KeyCode.LeftShift) ? fastMoveMultiplier : 1f;
        
        // Scale movement speed based on zoom level to maintain consistent screen speed
        float zoomScale = _camera.orthographicSize / minZoom;
        float speed = moveSpeed * multiplier * zoomScale * Time.deltaTime;
        
        Vector3 movement = Vector3.zero;
        
        // WASD movement
        if (Input.GetKey(KeyCode.W)) movement.z += 1;
        if (Input.GetKey(KeyCode.S)) movement.z -= 1;
        if (Input.GetKey(KeyCode.A)) movement.x -= 1;
        if (Input.GetKey(KeyCode.D)) movement.x += 1;
        
        if (movement != Vector3.zero)
        {
            _targetPosition += movement.normalized * speed;
        }
        
        transform.position = Vector3.Lerp(transform.position, _targetPosition, movementSmoothness);
    }

    private void HandleZoom()
    {
        float scrollDelta = Input.mouseScrollDelta.y;
        if (scrollDelta != 0)
        {
            if (Input.GetKey(KeyCode.LeftShift) && chunkManager != null)
            {
                // Y-Level control via scroll
                int yLevelChange = scrollDelta > 0 ? 1 : -1;
                int newYLevel = Mathf.Clamp(chunkManager.ViewMaxYLevel + yLevelChange, minYLevel, maxYLevel);
                chunkManager.ViewMaxYLevel = newYLevel;
                
                // Update slider to match
                if (yLevelSlider != null)
                {
                    yLevelSlider.value = newYLevel;
                }
            }
            else
            {
                // Regular zoom control
                float currentZoomPercent = (_camera.orthographicSize - minZoom) / (maxZoom - minZoom);
                float currentZoomSpeed = _camera.orthographicSize * zoomSpeed;
                
                if (scrollDelta > 0)
                    _targetZoom -= currentZoomSpeed;
                else
                    _targetZoom += currentZoomSpeed;
                
                _targetZoom = Mathf.Clamp(_targetZoom, minZoom, maxZoom);
            }
        }
        
        _camera.orthographicSize = Mathf.Lerp(_camera.orthographicSize, _targetZoom, zoomSmoothness);
    }
} 