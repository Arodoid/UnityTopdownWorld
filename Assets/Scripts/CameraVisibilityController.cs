using UnityEngine;
using UnityEngine.UI;

public class CameraVisibilityController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 20f;
    [SerializeField] private float zoomSpeed = 2f;
    [SerializeField] private float minZoom = 5f;
    [SerializeField] private float maxZoom = 30f;
    [SerializeField] private Text yLevelText;
    [SerializeField] private float updateThresholdDistance = 8f; // Distance in world units before triggering update
    
    private Camera mainCamera;
    private ChunkQueueProcessor chunkProcessor;
    private int currentYLevel = WorldDataManager.WORLD_MAX_Y;
    private Vector3 lastUpdatePosition;
    private float lastUpdateSize;

    public struct ViewData
    {
        public int YLevel;
        public Bounds ViewBounds;
    }

    private void Start()
    {
        mainCamera = GetComponent<Camera>();
        chunkProcessor = FindAnyObjectByType<ChunkQueueProcessor>();
        
        if (mainCamera == null || chunkProcessor == null)
        {
            Debug.LogError("Missing required components!");
            enabled = false;
            return;
        }
        
        // Initialize last update position
        lastUpdatePosition = transform.position;
        lastUpdateSize = mainCamera.orthographicSize;
        UpdateWorld();
    }

    private void Update()
    {
        bool needsUpdate = false;
        Vector3 currentPos = transform.position;

        // Batch movement checks
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        
        if (horizontal != 0f || vertical != 0f)
        {
            Vector3 movement = new Vector3(horizontal, 0f, vertical) * (moveSpeed * Time.deltaTime);
            transform.Translate(movement, Space.World);
            
            // Use sqrMagnitude instead of Distance for performance
            if ((currentPos - lastUpdatePosition).sqrMagnitude >= updateThresholdDistance * updateThresholdDistance)
            {
                needsUpdate = true;
            }
        }

        // Handle zoom and Y-level with early exits
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            if (Input.GetKey(KeyCode.LeftShift))
            {
                int newYLevel = Mathf.Clamp(currentYLevel + (int)Mathf.Sign(scroll), 
                    WorldDataManager.WORLD_MIN_Y, WorldDataManager.WORLD_MAX_Y);
                    
                if (newYLevel != currentYLevel)
                {
                    currentYLevel = newYLevel;
                    if (yLevelText) yLevelText.text = $"Y: {currentYLevel}";
                    needsUpdate = true;
                }
            }
            else
            {
                float newSize = Mathf.Clamp(
                    mainCamera.orthographicSize - scroll * zoomSpeed,
                    minZoom,
                    maxZoom
                );
                
                // Only update if change is significant
                if (Mathf.Abs(newSize - mainCamera.orthographicSize) > minZoom * 0.1f)
                {
                    mainCamera.orthographicSize = newSize;
                    needsUpdate = true;
                }
            }
        }

        if (needsUpdate)
        {
            UpdateWorld();
            lastUpdatePosition = transform.position;
            lastUpdateSize = mainCamera.orthographicSize;
        }
    }

    private void UpdateWorld()
    {
        // Calculate view bounds with a generous margin
        float margin = Chunk.ChunkSize * 3f;
        float width = mainCamera.orthographicSize * 2f * mainCamera.aspect + margin;
        float height = mainCamera.orthographicSize * 2f + margin;

        Bounds viewBounds = new Bounds(
            transform.position,
            new Vector3(width, 1000f, height)
        );

        ViewData viewData = new ViewData
        {
            YLevel = currentYLevel,
            ViewBounds = viewBounds
        };
        chunkProcessor.UpdateVisibleChunks(viewData);
    }
}