using UnityEngine;

public class BlockRegistry : MonoBehaviour
{
    [SerializeField] private BlockSprites blockSprites;

    private void Awake()
    {
        if (blockSprites == null)
        {
            Debug.LogError("BlockSprites asset not assigned to BlockRegistry!");
            return;
        }

        Block.Types.Initialize(blockSprites);
    }
} 