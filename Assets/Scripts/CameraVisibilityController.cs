using UnityEngine;
using UnityEngine.UI;
using VoxelGame.Utilities;

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

    // Add these fields for optimization
    private Vector3Int[] cachedChunkPositions;
    private int cachedChunkCount;
    private readonly int maxCachedChunks = 1024;

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
        
        // Measure bounds calculation
        float margin = Chunk.ChunkSize * 3f;
        float width = mainCamera.orthographicSize * 2f * mainCamera.aspect + margin;
        float height = mainCamera.orthographicSize * 2f + margin;

        Bounds viewBounds = new Bounds(
            transform.position,
            new Vector3(width, 1000f, height)
        );

        // Measure chunk position calculations
        if (cachedChunkPositions == null || cachedChunkPositions.Length != maxCachedChunks)
        {
            cachedChunkPositions = new Vector3Int[maxCachedChunks];
        }

        int minX = Mathf.FloorToInt((viewBounds.min.x - margin) / Chunk.ChunkSize);
        int maxX = Mathf.CeilToInt((viewBounds.max.x + margin) / Chunk.ChunkSize);
        int minZ = Mathf.FloorToInt((viewBounds.min.z - margin) / Chunk.ChunkSize);
        int maxZ = Mathf.CeilToInt((viewBounds.max.z + margin) / Chunk.ChunkSize);

        Vector2Int center = new Vector2Int(
            Mathf.RoundToInt(transform.position.x / Chunk.ChunkSize),
            Mathf.RoundToInt(transform.position.z / Chunk.ChunkSize)
        );

        cachedChunkCount = 0;
        // Simple spiral pattern from center
        int radius = 0;
        int maxRadius = Mathf.Max(maxX - minX, maxZ - minZ);
        
        while (radius <= maxRadius && cachedChunkCount < maxCachedChunks)
        {
            for (int dx = -radius; dx <= radius; dx++)
            for (int dz = -radius; dz <= radius; dz++)
            {
                // Only process edge of spiral
                if (Mathf.Abs(dx) != radius && Mathf.Abs(dz) != radius) continue;
                
                int x = center.x + dx;
                int z = center.y + dz;
                
                // Check bounds
                if (x < minX || x > maxX || z < minZ || z > maxZ) continue;

                if (cachedChunkCount < maxCachedChunks) // Ensure we don't exceed array bounds
                {
                    cachedChunkPositions[cachedChunkCount++] = new Vector3Int(x, 0, z);
                }
            }
            radius++;
        }


        // Measure chunk processing
        ViewData viewData = new ViewData
        {
            YLevel = currentYLevel,
            ViewBounds = viewBounds
        };

        chunkProcessor.UpdateVisibleChunks(viewData);

    }
}