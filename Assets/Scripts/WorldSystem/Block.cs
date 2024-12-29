using UnityEngine;

/// <summary>
/// Represents a block type in the world. This is just data - no behavior.
/// </summary>
public class Block
{
    public string Name { get; }
    public bool IsOpaque { get; }
    public Color32 Color { get; }
    public BlockRenderType RenderType { get; }
    public Sprite Sprite { get; }  // Optional sprite reference

    private Block(string name, bool isOpaque, Color32 color, BlockRenderType renderType, Sprite sprite = null)
    {
        Name = name;
        IsOpaque = isOpaque;
        Color = color;
        RenderType = renderType;
        Sprite = sprite;
    }

    // Block definitions
    public static class Types
    {
        // These will be set by BlockRegistry at runtime
        public static Block Stone { get; private set; }
        public static Block Dirt { get; private set; }
        public static Block Grass { get; private set; }
        public static Block TallGrass { get; private set; }
        public static Block Flower { get; private set; }

        // Initialize block types with their sprites
        public static void Initialize(BlockSprites sprites)
        {
            // Solid color blocks (no sprites needed)
            Stone = new Block("Stone", true, new Color32(128, 128, 128, 255), BlockRenderType.Cube);
            Dirt = new Block("Dirt", true, new Color32(133, 87, 35, 255), BlockRenderType.Cube);
            Grass = new Block("Grass", true, new Color32(86, 176, 0, 255), BlockRenderType.Cube);

            // Sprite-based blocks
            TallGrass = new Block("Tall Grass", false, UnityEngine.Color.white, BlockRenderType.Flat, sprites.TallGrass);
            Flower = new Block("Red Flower", false, UnityEngine.Color.white, BlockRenderType.Flat, sprites.Flower);
        }
    }
}

public enum BlockRenderType
{
    Cube,   // Full 3D cube
    Flat    // Vertical quad (for vegetation)
}