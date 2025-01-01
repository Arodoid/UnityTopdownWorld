using UnityEngine;
using UnityEngine.U2D;

public class Block
{
    public string Name { get; }
    public bool IsOpaque { get; }
    public Color32 Color { get; }
    public BlockRenderType RenderType { get; }
    public Vector2[] UVs { get; private set; }
    public bool UseRandomRotation { get; }

    private Block(string name, bool isOpaque, Color32 fallbackColor, BlockRenderType type = BlockRenderType.Cube, bool rotate = false)
    {
        Name = name;
        IsOpaque = isOpaque;
        Color = fallbackColor;
        RenderType = type;
        UseRandomRotation = rotate;
    }

    internal void InitializeUVs()
    {
        // Get UVs for this block's sprite
        UVs = BlockRegistry.GetUVsForSprite(Name.ToLower());
        
        if (UVs == null)
        {
            Debug.LogError($"Failed to get UVs for block: {Name}");
            // Provide default UVs as fallback
            UVs = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(1, 1),
                new Vector2(0, 1)
            };
        }
    }

    public static class Types
    {
        public static Block Stone = new("Stone", true, new Color32(128, 128, 128, 255), rotate: true);
        public static Block Dirt = new("Dirt", true, new Color32(133, 87, 35, 255), rotate: true);
        public static Block Gravel = new("Gravel", true, new Color32(128, 128, 128, 255), rotate: true);
        public static Block Coal = new("Coal", true, new Color32(128, 128, 128, 255), rotate: true);
        public static Block Grass = new("Grass", true, new Color32(86, 125, 70, 255), rotate: true);
        public static Block TallGrass = new("TallGrass", false, new Color32(106, 170, 64, 255), BlockRenderType.Flat);
        public static Block Flower = new("Flower", false, new Color32(255, 255, 100, 255), BlockRenderType.Flat);
        public static Block Sand = new("Sand", true, new Color32(219, 211, 160, 255), rotate: true);
        public static Block Sandstone = new("Sandstone", true, new Color32(219, 211, 160, 255), rotate: true);
        public static Block Snow = new("Snow", true, new Color32(250, 250, 250, 255), rotate: true);

        public static void Initialize(SpriteAtlas atlas)
        {
            if (atlas == null)
            {
                Debug.LogError("Cannot initialize block types: Atlas is null!");
                return;
            }

            var fields = typeof(Types).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            foreach (var field in fields)
            {
                if (field.FieldType == typeof(Block))
                {
                    var block = (Block)field.GetValue(null);
                    block.InitializeUVs();
                }
            }
            Debug.Log($"Initialized UVs for {fields.Length} block types");
        }
    }
}

public enum BlockRenderType { Cube, Flat }