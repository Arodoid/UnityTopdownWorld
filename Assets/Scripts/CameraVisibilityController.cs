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
    
    [Header("Debug")]
    [SerializeField] private bool debugVisualization = true;
    
    private Camera mainCamera;
    private ChunkQueueProcessor chunkProcessor;
    private EntityObjectManager entityManager;
    private Vector3Int lastChunkPosition;
    private float updateTimer;

    private void Start()
    {
        mainCamera = GetComponent<Camera>();
        chunkProcessor = Object.FindAnyObjectByType<ChunkQueueProcessor>();
        entityManager = Object.FindAnyObjectByType<EntityObjectManager>();
        UpdateVisibleWorld();
    }

    private void Update()
    {
        HandleMovement();
        HandleZoom();
        
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

    private void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            float newSize = mainCamera.orthographicSize - scroll * zoomSpeed;
            mainCamera.orthographicSize = Mathf.Clamp(newSize, minOrthographicSize, maxOrthographicSize);
            UpdateVisibleWorld();
            updateTimer = 0f;
        }
    }

    private int CalculateViewRadius()
    {
        float zoomFactor = mainCamera.orthographicSize / maxOrthographicSize;
        return Mathf.RoundToInt(zoomFactor * Chunk.ChunkSize);
    }

    private void UpdateVisibleWorld()
    {
        Vector3Int currentChunkPosition = CoordinateUtility.WorldToChunkPosition(transform.position);
        
        // Calculate view bounds based on camera's orthographic size
        float viewWidth = mainCamera.orthographicSize * 2 * mainCamera.aspect;
        float viewHeight = mainCamera.orthographicSize * 2;
        
        // Convert to chunk-space radius (using the larger of width/height)
        int chunkRadius = Mathf.CeilToInt(Mathf.Max(viewWidth, viewHeight) / Chunk.ChunkSize);
        
        if (currentChunkPosition != lastChunkPosition)
        {
            chunkProcessor.ScanChunksAsync(currentChunkPosition);
            lastChunkPosition = currentChunkPosition;
        }

        // Now send chunk-space coordinates and radius to entity manager
        entityManager.UpdateVisibleEntities(currentChunkPosition, chunkRadius);
    }
}