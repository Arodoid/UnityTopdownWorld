using UnityEngine;
using UnityEngine.UI;

public class CameraVisibilityController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 20f;
    [SerializeField] private float zoomSpeed = 2f;
    [SerializeField] private float minZoom = 5f;
    [SerializeField] private float maxZoom = 30f;
    [SerializeField] private Text yLevelText;
    
    private Camera mainCamera;
    private ChunkQueueProcessor chunkProcessor;
    private int currentYLevel = WorldDataManager.WORLD_MAX_Y;

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
        
        // Force initial update
        UpdateWorld();
    }

    private void Update()
    {
        bool needsUpdate = false;

        // Handle movement
        Vector3 movement = new Vector3(
            Input.GetAxis("Horizontal"),
            0f,
            Input.GetAxis("Vertical")
        ) * (moveSpeed * Time.deltaTime);
        
        if (movement != Vector3.zero)
        {
            transform.Translate(movement, Space.World);
            needsUpdate = true;
        }

        // Handle zoom and Y-level
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            if (Input.GetKey(KeyCode.LeftShift))
            {
                currentYLevel = Mathf.Clamp(currentYLevel + (int)Mathf.Sign(scroll), 
                    WorldDataManager.WORLD_MIN_Y, WorldDataManager.WORLD_MAX_Y);
                if (yLevelText) yLevelText.text = $"Y: {currentYLevel}";
            }
            else
            {
                mainCamera.orthographicSize = Mathf.Clamp(
                    mainCamera.orthographicSize - scroll * zoomSpeed,
                    minZoom,
                    maxZoom
                );
            }
            needsUpdate = true;
        }

        if (needsUpdate)
        {
            UpdateWorld();
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

        Debug.Log($"Camera requesting update: Pos={transform.position}, Size={mainCamera.orthographicSize}, Bounds={viewBounds.min}-{viewBounds.max}");
        chunkProcessor.UpdateVisibleChunks(viewData);
    }
}