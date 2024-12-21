using UnityEngine;

public class BlockInteractionSystem : MonoBehaviour
{
    [SerializeField] private float maxInteractionDistance = 10f;
    [SerializeField] private LayerMask blockLayerMask;
    [SerializeField] private byte blockTypeToPlace = BlockWorld.DIRT;
    private Camera mainCamera;

    private void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("BlockInteractionSystem: No main camera found!");
            enabled = false;
            return;
        }

        Debug.Log($"BlockInteractionSystem initialized with layer mask: {blockLayerMask.value}");
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log("Left click detected");
            HandleBlockBreak();
        }
        else if (Input.GetMouseButtonDown(1))
        {
            Debug.Log("Right click detected");
            HandleBlockPlace();
        }
    }

    private void HandleBlockBreak()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        Debug.DrawRay(ray.origin, ray.direction * maxInteractionDistance, Color.red, 1f);

        if (Physics.Raycast(ray, out RaycastHit hit, maxInteractionDistance, blockLayerMask))
        {
            Debug.Log($"Hit object: {hit.collider.gameObject.name} at position: {hit.point}");
            
            // Convert hit point to block coordinates
            Vector3 hitBlock = hit.point - (hit.normal * 0.001f);
            Vector3Int blockPos = new Vector3Int(
                Mathf.FloorToInt(hitBlock.x),
                Mathf.FloorToInt(hitBlock.y),
                Mathf.FloorToInt(hitBlock.z)
            );

            // Get chunk coordinates
            Vector2Int chunkPos = new Vector2Int(
                Mathf.FloorToInt(blockPos.x / ChunkData.CHUNK_SIZE),
                Mathf.FloorToInt(blockPos.z / ChunkData.CHUNK_SIZE)
            );

            Debug.Log($"Raw hit position: {hitBlock}");
            Debug.Log($"Attempting to break block at position: {blockPos}");
            Debug.Log($"In chunk: {chunkPos}");
            Debug.Log($"Hit normal: {hit.normal}");
            
            // Break the block
            BlockWorld.Instance.SetBlock(blockPos, BlockWorld.AIR);
            
            // Force chunk update
            BlockWorld.Instance.UpdateChunkMesh(chunkPos);
            
            // Also update neighboring chunks if we're on a chunk border
            Vector3Int localPos = new Vector3Int(
                ((blockPos.x % ChunkData.CHUNK_SIZE) + ChunkData.CHUNK_SIZE) % ChunkData.CHUNK_SIZE,
                blockPos.y,
                ((blockPos.z % ChunkData.CHUNK_SIZE) + ChunkData.CHUNK_SIZE) % ChunkData.CHUNK_SIZE
            );

            // Update neighboring chunks if we're on their borders
            if (localPos.x == 0) BlockWorld.Instance.UpdateChunkMesh(chunkPos + Vector2Int.left);
            if (localPos.x == ChunkData.CHUNK_SIZE - 1) BlockWorld.Instance.UpdateChunkMesh(chunkPos + Vector2Int.right);
            if (localPos.z == 0) BlockWorld.Instance.UpdateChunkMesh(chunkPos + Vector2Int.down);
            if (localPos.z == ChunkData.CHUNK_SIZE - 1) BlockWorld.Instance.UpdateChunkMesh(chunkPos + Vector2Int.up);
            
            byte blockType = BlockWorld.Instance.GetBlock(blockPos);
            Debug.Log($"Block type after break attempt: {blockType}");
        }
        else
        {
            Debug.Log("No block hit - Ray missed or layer mask mismatch");
        }
    }

    private void HandleBlockPlace()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        Debug.DrawRay(ray.origin, ray.direction * maxInteractionDistance, Color.blue, 1f);

        if (Physics.Raycast(ray, out RaycastHit hit, maxInteractionDistance, blockLayerMask))
        {
            Debug.Log($"Hit object: {hit.collider.gameObject.name} at position: {hit.point}");
            
            // Convert hit point to block coordinates
            Vector3 placePos = hit.point + (hit.normal * 0.001f);
            Vector3Int blockPos = new Vector3Int(
                Mathf.FloorToInt(placePos.x),
                Mathf.FloorToInt(placePos.y),
                Mathf.FloorToInt(placePos.z)
            );

            // Get chunk coordinates
            Vector2Int chunkPos = new Vector2Int(
                Mathf.FloorToInt(blockPos.x / ChunkData.CHUNK_SIZE),
                Mathf.FloorToInt(blockPos.z / ChunkData.CHUNK_SIZE)
            );

            Debug.Log($"Raw place position: {placePos}");
            Debug.Log($"Attempting to place block at position: {blockPos}");
            Debug.Log($"In chunk: {chunkPos}");
            Debug.Log($"Hit normal: {hit.normal}");
            
            // Place the block
            BlockWorld.Instance.SetBlock(blockPos, blockTypeToPlace);
            
            // Force chunk update
            BlockWorld.Instance.UpdateChunkMesh(chunkPos);
            
            // Also update neighboring chunks if we're on a chunk border
            Vector3Int localPos = new Vector3Int(
                ((blockPos.x % ChunkData.CHUNK_SIZE) + ChunkData.CHUNK_SIZE) % ChunkData.CHUNK_SIZE,
                blockPos.y,
                ((blockPos.z % ChunkData.CHUNK_SIZE) + ChunkData.CHUNK_SIZE) % ChunkData.CHUNK_SIZE
            );

            // Update neighboring chunks if we're on their borders
            if (localPos.x == 0) BlockWorld.Instance.UpdateChunkMesh(chunkPos + Vector2Int.left);
            if (localPos.x == ChunkData.CHUNK_SIZE - 1) BlockWorld.Instance.UpdateChunkMesh(chunkPos + Vector2Int.right);
            if (localPos.z == 0) BlockWorld.Instance.UpdateChunkMesh(chunkPos + Vector2Int.down);
            if (localPos.z == ChunkData.CHUNK_SIZE - 1) BlockWorld.Instance.UpdateChunkMesh(chunkPos + Vector2Int.up);
            
            byte blockType = BlockWorld.Instance.GetBlock(blockPos);
            Debug.Log($"Block type after place attempt: {blockType}");
        }
        else
        {
            Debug.Log("No block hit - Ray missed or layer mask mismatch");
        }
    }

    public void SetBlockTypeToPlace(byte newBlockType)
    {
        blockTypeToPlace = newBlockType;
        Debug.Log($"Block type to place changed to: {blockTypeToPlace}");
    }
} 