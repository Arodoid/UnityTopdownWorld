using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float zoomSpeed = 50f; // Speed of zoom movement
    private WorldManager worldManager;

    private void Start()
    {
        // Wait for WorldManager to initialize
        Invoke("Initialize", 0.1f);
    }

    private void Initialize()
    {
        worldManager = WorldManager.Instance;
        if (worldManager == null)
        {
            Debug.LogWarning("WorldManager not found, retrying in 0.1 seconds...");
            Invoke("Initialize", 0.1f);
            return;
        }
    }

    private void Update()
    {
        // Skip if WorldManager isn't ready
        if (worldManager == null) return;

        // Get input axes
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        // Calculate movement vector (moving in XZ plane now)
        Vector3 movement = new Vector3(horizontal, 0, vertical);
        
        // Move the camera
        transform.position += movement * moveSpeed * Time.deltaTime;

        // Handle zoom with scroll wheel
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            // Move camera along its forward direction
            transform.position += transform.forward * (scroll * zoomSpeed);
        }
    }
}