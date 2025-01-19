using UnityEngine;
using Unity.Mathematics;
using UISystem.API;
using UISystem.Core.Interactions;
using WorldSystem.API;
using WorldSystem.Data;
using System.Threading.Tasks;

namespace UISystem.Core.Tools
{
    public class BlockTool : IUITool
    {
        private BlockType _blockTypeToPlace = BlockType.Stone;
        private readonly BlockHighlighter _blockHighlighter;
        private readonly WorldSystemAPI _worldAPI;
        private readonly WorldCoordinateMapper _coordinateMapper;
        private int _brushSize = 1; // Default 1x1
        
        public string ToolId => "BlockTool";

        public BlockTool(WorldSystemAPI worldAPI, WorldCoordinateMapper coordinateMapper, BlockHighlighter blockHighlighter)
        {
            _worldAPI = worldAPI;
            _coordinateMapper = coordinateMapper;
            _blockHighlighter = blockHighlighter;
        }

        public void SetBlockType(BlockType blockType)
        {
            _blockTypeToPlace = blockType;
        }

        public void SetBrushSize(int size)
        {
            _brushSize = Mathf.Clamp(size, 1, 5); // Limit brush size between 1 and 5
        }

        public void OnToolActivated()
        {
            Debug.Log("Block tool activated");
            _blockHighlighter.ShowHighlight();
        }

        public void OnToolDeactivated()
        {
            _blockHighlighter.HideHighlight();
        }

        public async void OnPointerDown(Vector2 position)
        {
            await HandleBlockAction(position);
        }

        public async void OnPointerDragged(Vector2 position)
        {
            // Enable continuous block placement/removal while dragging
            await HandleBlockAction(position);
        }

        private async Task HandleBlockAction(Vector2 position)
        {
            if (_blockHighlighter.GetHighlightedPosition() is int3 centerPos)
            {
                int offset = (_brushSize - 1) / 2;
                
                // Apply action to all blocks within brush size
                for (int x = -offset; x <= offset; x++)
                {
                    for (int z = -offset; z <= offset; z++)
                    {
                        if (Input.GetMouseButton(0)) // Left click/drag
                        {
                            int3 placePosition = new int3(
                                centerPos.x + x,
                                centerPos.y + 1,
                                centerPos.z + z
                            );
                            await _worldAPI.SetBlock(placePosition, _blockTypeToPlace);
                        }
                        else if (Input.GetMouseButton(1)) // Right click/drag
                        {
                            int3 removePosition = new int3(
                                centerPos.x + x,
                                centerPos.y,
                                centerPos.z + z
                            );
                            await _worldAPI.SetBlock(removePosition, BlockType.Air);
                        }
                    }
                }
            }
        }

        public void OnPointerUp(Vector2 position)
        {
            // Not needed for this tool
        }

        public void OnPointerMoved(Vector2 position)
        {
            _blockHighlighter.UpdateHighlight(position);
        }
    }
} 