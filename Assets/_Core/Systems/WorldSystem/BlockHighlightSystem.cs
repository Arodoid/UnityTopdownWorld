using UnityEngine;

public class BlockHighlightSystem : MonoBehaviour
{
    [SerializeField] private Material highlightMaterial;
    [SerializeField] private float maxHighlightDistance = 10f;
    [SerializeField] private LayerMask blockLayerMask;
    
    private Camera mainCamera;
    private GameObject highlightCube;

    private void Start()
    {
        mainCamera = Camera.main;
        CreateHighlightCube();
    }

    private void CreateHighlightCube()
    {
        highlightCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        highlightCube.GetComponent<MeshRenderer>().material = highlightMaterial;
        highlightCube.GetComponent<Collider>().enabled = false;
        highlightCube.transform.localScale = new Vector3(1.01f, 1.01f, 1.01f); // Slightly larger than blocks
        highlightCube.SetActive(false);
    }

    private void Update()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, maxHighlightDistance, blockLayerMask))
        {
            Vector3 hitBlock = hit.point - (hit.normal * 0.5f);
            Vector3Int blockPos = new Vector3Int(
                Mathf.FloorToInt(hitBlock.x),
                Mathf.FloorToInt(hitBlock.y),
                Mathf.FloorToInt(hitBlock.z)
            );
            
            highlightCube.SetActive(true);
            highlightCube.transform.position = new Vector3(blockPos.x + 0.5f, blockPos.y + 0.5f, blockPos.z + 0.5f);
        }
        else
        {
            highlightCube.SetActive(false);
        }
    }
} 