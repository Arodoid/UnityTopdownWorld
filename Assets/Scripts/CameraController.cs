using UnityEngine;

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

        _targetPosition = transform.position;
        _targetZoom = _camera.orthographicSize;
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
            // Calculate zoom percentage (0 to 1) where 0 is minZoom and 1 is maxZoom
            float currentZoomPercent = (_camera.orthographicSize - minZoom) / (maxZoom - minZoom);
            
            // Base zoom speed that scales with the current size
            float currentZoomSpeed = _camera.orthographicSize * zoomSpeed;
            
            // Apply the zoom change
            if (scrollDelta > 0) // Zooming in
            {
                _targetZoom -= currentZoomSpeed;
            }
            else // Zooming out
            {
                _targetZoom += currentZoomSpeed;
            }
            
            _targetZoom = Mathf.Clamp(_targetZoom, minZoom, maxZoom);
        }
        
        _camera.orthographicSize = Mathf.Lerp(_camera.orthographicSize, _targetZoom, zoomSmoothness);
    }
} 