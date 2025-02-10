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
    
    [Header("Camera Rotation")]
    [SerializeField] private float rotationSpeed = 2f;
    [SerializeField] private float quickRotationSpeed = 400f;
    [SerializeField] private float quickRotationSmoothness = 0.1f;
    [SerializeField] private float orbitDistance = 50f;
    [SerializeField] private float minVerticalAngle = -80f;
    [SerializeField] private float maxVerticalAngle = 80f;
    
    private Camera _camera;
    private Vector3 _targetPosition;
    private float _targetZoom;
    private float _rotationX = 0f;
    private float _rotationY = 0f;
    private float _targetRotationY = 0f;
    private Vector3 _pivotPoint;
    private bool _isOrbiting = false;
    
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

        // Ensure far clipping plane is set for maximum view distance
        _camera.farClipPlane = 100000f;
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
        HandleRotation();
        HandleZoom();
    }

    private void HandleRotation()
    {
        // Quick 90-degree orbit with Q/E
        if (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.E))
        {
            // Calculate pivot point using current camera orientation
            _pivotPoint = transform.position + transform.rotation * Vector3.forward * orbitDistance;
            _targetRotationY += Input.GetKeyDown(KeyCode.Q) ? -90f : 90f;
            _isOrbiting = true;
        }

        // Start orbiting when right mouse is first pressed
        if (Input.GetMouseButtonDown(1))
        {
            _pivotPoint = transform.position + transform.rotation * Vector3.forward * orbitDistance;
            _isOrbiting = true;
        }

        if (_isOrbiting)
        {
            if (Input.GetMouseButton(1)) // Update target rotation during right-click drag
            {
                float mouseX = Input.GetAxis("Mouse X") * rotationSpeed;
                float mouseY = Input.GetAxis("Mouse Y") * rotationSpeed;
                
                _targetRotationY += mouseX;
                _rotationX -= mouseY;
                _rotationX = Mathf.Clamp(_rotationX, minVerticalAngle, maxVerticalAngle);
            }

            // Smooth rotation towards target angle
            _rotationY = Mathf.LerpAngle(_rotationY, _targetRotationY, quickRotationSmoothness);
            
            // Calculate new position based on orbit, maintaining initial orientation
            Quaternion initialRotation = Quaternion.Euler(transform.eulerAngles.x, 0, transform.eulerAngles.z);
            Quaternion orbitRotation = Quaternion.Euler(0, _rotationY, 0);
            Vector3 orbitPosition = _pivotPoint - (orbitRotation * initialRotation * Vector3.forward * orbitDistance);
            
            _targetPosition = orbitPosition;
            
            // Only modify the Y rotation, preserve other axes
            Vector3 currentRotation = transform.eulerAngles;
            currentRotation.y = _rotationY;
            transform.eulerAngles = currentRotation;

            // Stop orbiting when right mouse is released or we reach Q/E target
            if (!Input.GetMouseButton(1) && Mathf.Abs(Mathf.DeltaAngle(_rotationY, _targetRotationY)) < 0.01f)
            {
                _isOrbiting = false;
            }
        }
    }

    private void HandleMovement()
    {
        float multiplier = Input.GetKey(KeyCode.LeftShift) ? fastMoveMultiplier : 1f;
        float speed = moveSpeed * multiplier * Time.deltaTime;
        
        Vector3 movement = Vector3.zero;
        
        float yaw = transform.eulerAngles.y;
        Quaternion rotation = Quaternion.Euler(0, yaw, 0);
        
        if (Input.GetKey(KeyCode.W)) movement += rotation * Vector3.forward;
        if (Input.GetKey(KeyCode.S)) movement += rotation * Vector3.back;
        if (Input.GetKey(KeyCode.A)) movement += rotation * Vector3.left;
        if (Input.GetKey(KeyCode.D)) movement += rotation * Vector3.right;
        if (Input.GetKey(KeyCode.R)) movement += Vector3.up;
        if (Input.GetKey(KeyCode.F)) movement += Vector3.down;
        
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
                int yLevelChange = scrollDelta > 0 ? 1 : -1;
                int newYLevel = Mathf.Clamp(chunkManager.ViewMaxYLevel + yLevelChange, minYLevel, maxYLevel);
                chunkManager.ViewMaxYLevel = newYLevel;
                
                if (yLevelSlider != null)
                {
                    yLevelSlider.value = newYLevel;
                }
            }
            else
            {
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