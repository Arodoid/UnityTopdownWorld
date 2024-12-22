using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float zoomSpeed = 2f;
    [SerializeField] private float minZoom = 5f;
    [SerializeField] private float maxZoom = 50f;
    [SerializeField] private WorldManager worldManager;

    private UnityEngine.Camera mainCamera;
    private Vector3 lastPosition;
    private float lastOrthoSize;

    private void Start()
    {
        mainCamera = GetComponent<UnityEngine.Camera>();
        if (mainCamera == null || worldManager == null)
        {
            Debug.LogError("Missing required components!");
            return;
        }

        mainCamera.orthographic = true;
        lastPosition = transform.position;
        lastOrthoSize = mainCamera.orthographicSize;
        
        ReportViewToManager();
    }

    private void Update()
    {
        HandleInput();
        CheckViewChanged();
    }

    private void HandleInput()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        if (horizontal != 0 || vertical != 0)
        {
            transform.position += new Vector3(horizontal, 0, vertical) * moveSpeed * Time.deltaTime;
        }

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            mainCamera.orthographicSize = Mathf.Clamp(
                mainCamera.orthographicSize - scroll * zoomSpeed,
                minZoom,
                maxZoom
            );
        }
    }

    private void CheckViewChanged()
    {
        if (Vector3.Distance(transform.position, lastPosition) > 0.1f || 
            Mathf.Abs(lastOrthoSize - mainCamera.orthographicSize) > 0.01f)
        {
            ReportViewToManager();
            lastPosition = transform.position;
            lastOrthoSize = mainCamera.orthographicSize;
        }
    }

    private void ReportViewToManager()
    {
        float height = mainCamera.orthographicSize * 2;
        float width = height * mainCamera.aspect;

        worldManager.UpdateWorldView(
            transform.position,  // where the camera is
            width,              // how wide the view is (based on zoom)
            height             // how tall the view is (based on zoom)
        );
    }
}
