using UnityEngine;
using UnityEngine.U2D;

public class Block
{
    public string Name { get; }
    public bool IsOpaque { get; }
    public Color32 Color { get; }
    public BlockRenderType RenderType { get; }
    public Vector2[] UVs { get; }
    public bool UseRandomRotation { get; }

    private Block(string name, bool isOpaque, Color32 fallbackColor, BlockRenderType type = BlockRenderType.Cube, bool rotate = false)
    {
        Name = name;
        IsOpaque = isOpaque;
        Color = fallbackColor;
        RenderType = type;
        UVs = BlockRegistry.GetUVsForSprite(name.ToLower());
        UseRandomRotation = rotate;
    }

    public static class Types
    {
        public static Block Stone = new("Stone", true, new Color32(128, 128, 128, 255), rotate: true);
        public static Block Dirt = new("Dirt", true, new Color32(133, 87, 35, 255), rotate: true);
        public static Block Grass = new("Grass", true, new Color32(86, 125, 70, 255), rotate: true);
        public static Block TallGrass = new("TallGrass", false, new Color32(106, 170, 64, 255), BlockRenderType.Flat);
        public static Block Flower = new("Flower", false, new Color32(255, 255, 100, 255), BlockRenderType.Flat);
        public static Block Sand = new("Sand", true, new Color32(219, 211, 160, 255), rotate: true);
        public static Block Snow = new("Snow", true, new Color32(250, 250, 250, 255), rotate: true);

        public static void Initialize(SpriteAtlas atlas) => Debug.Log("Blocks initialized!");
    }
}

public enum BlockRenderType { Cube, Flat }