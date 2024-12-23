using UnityEngine;

/// <summary>
/// Represents a single block type and its properties
/// </summary>
public readonly struct BlockType
{
    public readonly byte ID { get; }
    public readonly string Name { get; }
    public readonly Color Color { get; }
    public readonly bool IsSolid { get; }

    public BlockType(byte id, string name, Color color, bool isSolid = true)
    {
        ID = id;
        Name = name;
        Color = color;
        IsSolid = isSolid;
    }

    // Static block definitions
    public static readonly BlockType Air = new(0, "Air", Color.clear, false);
    public static readonly BlockType Dirt = new(1, "Dirt", new Color(0.6f, 0.4f, 0.2f));
    public static readonly BlockType Stone = new(2, "Stone", new Color(0.5f, 0.5f, 0.5f));
    public static readonly BlockType Grass = new(3, "Grass", new Color(0.3f, 0.8f, 0.2f));
    public static readonly BlockType Gravel = new(4, "Gravel", new Color(0.55f, 0.55f, 0.55f));
    // Quick lookup array
    private static readonly BlockType[] Types = { Air, Dirt, Stone, Grass, Gravel };

    public static BlockType FromID(byte id)
    {
        return id < Types.Length ? Types[id] : Air;
    }
} 