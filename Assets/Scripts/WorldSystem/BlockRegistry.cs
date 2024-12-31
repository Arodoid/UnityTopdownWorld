using UnityEngine;
using UnityEngine.U2D;

public class BlockRegistry : MonoBehaviour
{
    [SerializeField] private SpriteAtlas atlas;
    [SerializeField] private Shader terrainShader;
    private static Material terrainMaterial;
    private static SpriteAtlas blockAtlas;
    private static bool isInitialized = false;

    public static Material TerrainMaterial => terrainMaterial;
    public static bool IsInitialized => isInitialized;

    private void Awake()
    {
        Debug.Log("BlockRegistry Awake starting...");
        
        if (atlas == null)
        {
            Debug.LogError("Block Atlas not assigned!");
            return;
        }

        // Assign to static field
        blockAtlas = atlas;

        // Create shared terrain material
        if (terrainShader == null)
        {
            terrainShader = Shader.Find("Custom/TerrainShadowShader");
        }
        
        if (terrainShader == null)
        {
            Debug.LogError("Could not find TerrainShadowShader!");
            return;
        }

        terrainMaterial = new Material(terrainShader);
        
        // Set the texture atlas
        Sprite[] sprites = new Sprite[1];
        blockAtlas.GetSprites(sprites);
        if (sprites[0] != null)
        {
            terrainMaterial.mainTexture = sprites[0].texture;
            Debug.Log($"Set terrain material texture: {sprites[0].texture.width}x{sprites[0].texture.height}");
        }
        else
        {
            Debug.LogError("No sprites found in atlas!");
            return;
        }

        // Set initialized BEFORE initializing blocks
        isInitialized = true;

        // Initialize block types
        Block.Types.Initialize(blockAtlas);
        
        Debug.Log("BlockRegistry initialization complete!");
    }

    public static Vector2[] GetUVsForSprite(string spriteName)
    {
        if (!isInitialized)
        {
            Debug.LogError("BlockRegistry not initialized when requesting UVs!");
            return null;
        }

        if (blockAtlas == null)
        {
            Debug.LogError("Block Atlas not initialized!");
            return null;
        }

        Sprite sprite = blockAtlas.GetSprite(spriteName);
        if (sprite == null)
        {
            sprite = blockAtlas.GetSprite(spriteName + "(Clone)");
        }
        
        if (sprite == null)
        {
            Debug.LogError($"Could not find sprite: {spriteName} or {spriteName}(Clone)");
            return null;
        }

        // Simply return the sprite's UVs - Unity handles all the atlas calculations for us!
        return sprite.uv;
    }
} 