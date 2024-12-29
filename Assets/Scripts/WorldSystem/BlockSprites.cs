using UnityEngine;

[CreateAssetMenu(fileName = "BlockSprites", menuName = "VoxelGame/Block Sprites")]
public class BlockSprites : ScriptableObject
{
    [Header("Vegetation Sprites")]
    public Sprite TallGrass;
    public Sprite Flower;
    
    [Header("Block Textures")]
    public Sprite Stone;
    public Sprite Dirt;
    public Sprite Grass;
} 