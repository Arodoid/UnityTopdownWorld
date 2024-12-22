using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float zoomSpeed = 2f;
    [SerializeField] private float minZoom = 5f;
    [SerializeField] private float maxZoom = 50f;
    [SerializeField] private int viewRadius = 2; // In chunks
    [SerializeField] private WorldManager worldManager; // Direct reference

    private UnityEngine.Camera mainCamera;
    private Vector2Int lastChunkPos;

    private void Start()
    {
        mainCamera = GetComponent<UnityEngine.Camera>();
        if (mainCamera == null)
        {
            Debug.LogError("Camera component not found!");
            return;
        }

        if (worldManager == null)
        {
            Debug.LogError("WorldManager reference not set!");
            return;
        }

        mainCamera.orthographic = true;
        UpdateViewArea();
    }

    private void Update()
    {
        HandleInput();
        CheckPositionUpdate();
    }

    private void HandleInput()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        transform.position += new Vector3(horizontal, 0, vertical) * moveSpeed * Time.deltaTime;

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        mainCamera.orthographicSize = Mathf.Clamp(
            mainCamera.orthographicSize - scroll * zoomSpeed,
            minZoom,
            maxZoom
        );
    }

    private void CheckPositionUpdate()
    {
        Vector2Int currentChunkPos = WorldToChunkPos(transform.position);
        if (currentChunkPos != lastChunkPos)
        {
            UpdateViewArea();
            lastChunkPos = currentChunkPos;
        }
    }

    private void UpdateViewArea()
    {
        Vector2Int currentChunkPos = WorldToChunkPos(transform.position);
        
        // Calculate camera view dimensions
        float height = mainCamera.orthographicSize * 2;
        float width = height * mainCamera.aspect;
        Vector3 cameraPos = transform.position;
        
        // Calculate chunks needed based on camera view
        int chunksNeededX = Mathf.CeilToInt(width / ChunkData.CHUNK_SIZE);
        int chunksNeededZ = Mathf.CeilToInt(height / ChunkData.CHUNK_SIZE);
        
        // Use the larger of width or height to ensure we cover the entire visible area
        int radius = Mathf.Max(chunksNeededX, chunksNeededZ) / 2 + 1; // +1 for safety margin

        // Request the chunks - WorldManager will handle the vertical generation
        WorldManager.Instance.RequestChunkArea(currentChunkPos, radius, cameraPos);
        
        // Debug visualization of view volume
        DrawDebugViewVolume(cameraPos, width, height);
    }

    private void DrawDebugViewVolume(Vector3 cameraPos, float width, float height)
    {
        // Draw camera frustum for the full world height
        float worldHeight = ChunkData.WORLD_HEIGHT;
        Color debugColor = Color.red;
        
        // Bottom rectangle
        Debug.DrawLine(cameraPos + new Vector3(-width/2, 0, -height/2), cameraPos + new Vector3(width/2, 0, -height/2), debugColor);
        Debug.DrawLine(cameraPos + new Vector3(-width/2, 0, height/2), cameraPos + new Vector3(width/2, 0, height/2), debugColor);
        Debug.DrawLine(cameraPos + new Vector3(-width/2, 0, -height/2), cameraPos + new Vector3(-width/2, 0, height/2), debugColor);
        Debug.DrawLine(cameraPos + new Vector3(width/2, 0, -height/2), cameraPos + new Vector3(width/2, 0, height/2), debugColor);
        
        // Top rectangle
        Debug.DrawLine(cameraPos + new Vector3(-width/2, worldHeight, -height/2), cameraPos + new Vector3(width/2, worldHeight, -height/2), debugColor);
        Debug.DrawLine(cameraPos + new Vector3(-width/2, worldHeight, height/2), cameraPos + new Vector3(width/2, worldHeight, height/2), debugColor);
        Debug.DrawLine(cameraPos + new Vector3(-width/2, worldHeight, -height/2), cameraPos + new Vector3(-width/2, worldHeight, height/2), debugColor);
        Debug.DrawLine(cameraPos + new Vector3(width/2, worldHeight, -height/2), cameraPos + new Vector3(width/2, worldHeight, height/2), debugColor);
        
        // Vertical lines connecting top and bottom
        Debug.DrawLine(cameraPos + new Vector3(-width/2, 0, -height/2), cameraPos + new Vector3(-width/2, worldHeight, -height/2), debugColor);
        Debug.DrawLine(cameraPos + new Vector3(width/2, 0, -height/2), cameraPos + new Vector3(width/2, worldHeight, -height/2), debugColor);
        Debug.DrawLine(cameraPos + new Vector3(-width/2, 0, height/2), cameraPos + new Vector3(-width/2, worldHeight, height/2), debugColor);
        Debug.DrawLine(cameraPos + new Vector3(width/2, 0, height/2), cameraPos + new Vector3(width/2, worldHeight, height/2), debugColor);
    }

    private Vector2Int WorldToChunkPos(Vector3 worldPos)
    {
        const int CHUNK_SIZE = 16;
        return new Vector2Int(
            Mathf.FloorToInt(worldPos.x / CHUNK_SIZE),
            Mathf.FloorToInt(worldPos.z / CHUNK_SIZE)
        );
    }
}
