using UnityEngine;
using UnityEngine.U2D;

public class BlockRegistry : MonoBehaviour
{
    [SerializeField] private SpriteAtlas atlas;
    [SerializeField] private Shader terrainShader;
    private static Material terrainMaterial;
    private static SpriteAtlas blockAtlas;
    private static bool isInitialized = false;
    private static bool isInitializing = false;

    public static Material TerrainMaterial => terrainMaterial;
    public static bool IsInitialized => isInitialized;

    private void Awake()
    {
        Debug.Log("BlockRegistry Awake starting...");
        
        // Reset initialization state on Awake
        isInitialized = false;
        isInitializing = false;
        
        if (!ValidateReferences())
        {
            Debug.LogError("BlockRegistry initialization failed due to missing references!");
            return;
        }
        
        Initialize();
        Debug.Log("BlockRegistry Awake completed.");
    }

    private bool ValidateReferences()
    {
        if (atlas == null)
        {
            Debug.LogError("Block Atlas not assigned in BlockRegistry!");
            return false;
        }

        blockAtlas = atlas; // Store the atlas reference statically

        if (terrainShader == null)
        {
            terrainShader = Shader.Find("Custom/TerrainShadowShader");
            if (terrainShader == null)
            {
                Debug.LogError("Could not find TerrainShadowShader!");
                return false;
            }
        }

        return true;
    }

    public void Initialize()
    {
        if (isInitialized)
        {
            Debug.Log("BlockRegistry already initialized, skipping.");
            return;
        }

        if (isInitializing)
        {
            Debug.Log("BlockRegistry initialization in progress, skipping.");
            return;
        }

        isInitializing = true;

        try
        {
            Debug.Log("Creating terrain material...");
            terrainMaterial = new Material(terrainShader);
            
            // Get sprites from atlas with proper error handling
            Sprite[] sprites = new Sprite[atlas.spriteCount];
            atlas.GetSprites(sprites);
            
            if (sprites.Length == 0)
            {
                Debug.LogError("No sprites found in atlas!");
                isInitializing = false;
                return;
            }

            // Set the texture
            Texture2D atlasTexture = sprites[0].texture;
            terrainMaterial.mainTexture = atlasTexture;
            Debug.Log($"Set terrain material texture: {atlasTexture.width}x{atlasTexture.height}");

            // Initialize block types
            Block.Types.Initialize(atlas);
            Debug.Log("Block types initialized successfully!");
            
            isInitialized = true;
            isInitializing = false;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to initialize block types: {e.Message}");
            isInitialized = false;
            isInitializing = false;
        }
    }

    public static Vector2[] GetUVsForSprite(string spriteName)
    {
        if (blockAtlas == null)
        {
            Debug.LogError($"Block Atlas not initialized when getting UVs for {spriteName}!");
            return GetDefaultUVs();
        }

        Sprite sprite = blockAtlas.GetSprite(spriteName);
        if (sprite == null)
        {
            Debug.LogError($"Could not find sprite: {spriteName}");
            return GetDefaultUVs();
        }

        return sprite.uv;
    }

    private static Vector2[] GetDefaultUVs()
    {
        return new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(1, 1),
            new Vector2(0, 1)
        };
    }
} 