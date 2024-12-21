using UnityEngine;

public class WorldManager : MonoBehaviour
{
    public static WorldManager Instance { get; private set; }
    
    [SerializeField] private float cameraSpeed = 10f;
    [SerializeField] private float zoomSpeed = 2f;
    private Camera mainCamera;
    
    // Current view settings
    private int currentZLevel = 0;
    public int CurrentZLevel => currentZLevel;

    private void Awake()
    {
        Instance = this;
        mainCamera = Camera.main;
        
        // Match the inspector settings
        mainCamera.transform.position = new Vector3(0, 100, 0);
        mainCamera.transform.rotation = Quaternion.Euler(65, 0, 0);
        mainCamera.fieldOfView = 20f;
        
        // Set camera properties
        mainCamera.nearClipPlane = 0.3f;
        mainCamera.clearFlags = CameraClearFlags.Skybox;
        mainCamera.usePhysicalProperties = false;
        
        Debug.Log("World Manager initialized with angled camera view");
    }

    private void Update()
    {
        HandleCameraMovement();
        
        // Get current chunk position
        Vector2Int currentChunkPos = WorldToChunkPosition(mainCamera.transform.position);
        
        // Only update chunks if we've moved to a new chunk
        if (currentChunkPos != lastLoggedChunk)
        {
            UpdateVisibleChunks();
            lastLoggedChunk = currentChunkPos;
        }
    }

    private void HandleCameraMovement()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        Vector3 movement = new Vector3(horizontal, 0, vertical) * cameraSpeed * Time.deltaTime;
        mainCamera.transform.position += movement;

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        mainCamera.orthographicSize = Mathf.Clamp(
            mainCamera.orthographicSize - scroll * zoomSpeed,
            5f, 50f);
    }

    public void SetViewLevel(int level)
    {
        currentZLevel = Mathf.Clamp(level, 0, ChunkData.WORLD_DEPTH - 1);
    }

    public bool IsValidGridPosition(Vector3Int position)
    {
        // Only check Z height, X and Y are infinite
        return position.z >= 0 && position.z < (ChunkData.WORLD_DEPTH * ChunkData.CHUNK_SIZE);
    }

    public Vector3Int WorldToGridPosition(Vector3 worldPos)
    {
        return new Vector3Int(
            Mathf.FloorToInt(worldPos.x),
            Mathf.FloorToInt(worldPos.y),
            Mathf.FloorToInt(worldPos.z)
        );
    }

    private Vector2Int WorldToChunkPosition(Vector3 worldPos)
    {
        return new Vector2Int(
            Mathf.FloorToInt(worldPos.x / ChunkData.CHUNK_SIZE),
            Mathf.FloorToInt(worldPos.z / ChunkData.CHUNK_SIZE)
        );
    }

    private Vector2Int lastLoggedChunk = new Vector2Int(int.MaxValue, int.MaxValue);
    private void UpdateVisibleChunks()
    {
        Vector2Int currentChunkPos = WorldToChunkPosition(mainCamera.transform.position);
        
        // Only log when moving to a new chunk
        if (currentChunkPos != lastLoggedChunk)
        {
            Debug.Log($"Camera moved to chunk ({currentChunkPos.x}, {currentChunkPos.y})");
            lastLoggedChunk = currentChunkPos;
        }
        
        BlockWorld.Instance.UpdateVisibleChunks(currentChunkPos);
    }

    public bool IsChunkVisible(Vector2Int chunkPos)
    {
        if (mainCamera == null) return false;

        // Calculate chunk bounds
        Vector3 chunkMin = new Vector3(
            chunkPos.x * ChunkData.CHUNK_SIZE,
            0,  // Y is up now
            chunkPos.y * ChunkData.CHUNK_SIZE
        );
        Vector3 chunkMax = chunkMin + new Vector3(
            ChunkData.CHUNK_SIZE,
            ChunkData.CHUNK_SIZE * ChunkData.WORLD_DEPTH,  // Account for full height
            ChunkData.CHUNK_SIZE
        );
        
        // Create chunk bounds
        Bounds chunkBounds = new Bounds(
            (chunkMin + chunkMax) * 0.5f,  // Center
            chunkMax - chunkMin            // Size
        );
        
        // Check if chunk bounds intersect with camera frustum
        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(mainCamera);
        return GeometryUtility.TestPlanesAABB(planes, chunkBounds);
    }
} 