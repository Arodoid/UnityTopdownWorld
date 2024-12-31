using UnityEngine;
using VoxelGame.Entities;
using VoxelGame.Core.Debugging;

public class CameraVisibilityController : MonoBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private float moveSpeed = 20f;
    [SerializeField] private float zoomSpeed = 2f;
    [SerializeField] private float minOrthographicSize = 5f;
    [SerializeField] private float maxOrthographicSize = 30f;
    
    [Header("World Loading")]
    [SerializeField] private float updateInterval = 0.5f;
    
    [Header("Y-Level Control")]
    [SerializeField] private int defaultYLevel = int.MaxValue;
    [SerializeField] private int minYLevel = 0;
    [SerializeField] private int maxYLevel = 256;
    [SerializeField] private int yLevelScrollSpeed = 1; // Blocks per scroll
    [SerializeField] private int yLevelKeySpeed = 1;   // Blocks per key press
    private int currentYLevel;

    private Camera mainCamera;
    private ChunkQueueProcessor chunkProcessor;
    private EntityObjectManager entityManager;
    private Vector3Int lastChunkPosition;
    private float updateTimer;

    public struct ViewData
    {
        public Vector3Int CenterChunk;
        public int YLevel;
        public Bounds ViewBounds;
        public float ViewWidth;
        public float ViewHeight;
    }

    private void Start()
    {
        mainCamera = GetComponent<Camera>();
        chunkProcessor = Object.FindAnyObjectByType<ChunkQueueProcessor>();
        entityManager = Object.FindAnyObjectByType<EntityObjectManager>();
        currentYLevel = defaultYLevel;
        UpdateVisibleWorld();
    }

    private void Update()
    {
        HandleMovement();
        HandleZoomAndYLevel();
        
        updateTimer += Time.deltaTime;
        if (updateTimer >= updateInterval)
        {
            UpdateVisibleWorld();
            updateTimer = 0f;
        }
    }

    private void HandleMovement()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        Vector3 movement = new Vector3(horizontal, 0f, vertical) * (moveSpeed * Time.deltaTime);
        transform.Translate(movement, Space.World);
    }

    private void HandleZoomAndYLevel()
    {
        // Handle scroll wheel input
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                ChangeYLevel((int)Mathf.Sign(scroll) * yLevelScrollSpeed);
            }
            else
            {
                // Handle normal zoom
                float newSize = mainCamera.orthographicSize - scroll * zoomSpeed;
                mainCamera.orthographicSize = Mathf.Clamp(newSize, minOrthographicSize, maxOrthographicSize);
                UpdateVisibleWorld();
                updateTimer = 0f;
            }
        }

        // Handle keyboard input for Y-level
        if (Input.GetKeyDown(KeyCode.Plus) || 
            Input.GetKeyDown(KeyCode.KeypadPlus) || 
            Input.GetKeyDown(KeyCode.Equals)) // Adding equals key (usually shared with plus)
        {
            ChangeYLevel(yLevelKeySpeed);
        }
        else if (Input.GetKeyDown(KeyCode.Minus) || 
                 Input.GetKeyDown(KeyCode.KeypadMinus) || 
                 Input.GetKeyDown(KeyCode.Underscore)) // Adding underscore key (usually shared with minus)
        {
            ChangeYLevel(-yLevelKeySpeed);
        }
    }

    private void ChangeYLevel(int delta)
    {
        int newYLevel = Mathf.Clamp(currentYLevel + delta, minYLevel, maxYLevel);
        if (newYLevel != currentYLevel)
        {
            currentYLevel = newYLevel;
            UpdateVisibleWorld(); // Force immediate update
            updateTimer = 0f;
        }
    }

    private void UpdateVisibleWorld()
    {
        Vector3Int currentChunkPosition = CoordinateUtility.WorldToChunkPosition(transform.position);
        
        // Calculate view bounds based on camera's orthographic size
        float viewWidth = mainCamera.orthographicSize * 2 * mainCamera.aspect;
        float viewHeight = mainCamera.orthographicSize * 2;
        
        // Create view bounds
        Bounds viewBounds = new Bounds(
            transform.position,
            new Vector3(viewWidth, 1000f, viewHeight)
        );

        ViewData viewData = new ViewData
        {
            CenterChunk = currentChunkPosition,
            YLevel = currentYLevel,
            ViewBounds = viewBounds,
            ViewWidth = viewWidth,
            ViewHeight = viewHeight
        };

        // Pass all view data to chunk processor
        chunkProcessor.ScanChunksAsync(viewData);
        lastChunkPosition = currentChunkPosition;

        // Update entity manager with the same view data
        entityManager.UpdateVisibleEntities(currentChunkPosition, 
            Mathf.CeilToInt(Mathf.Max(viewWidth, viewHeight) / Chunk.ChunkSize));
    }
}