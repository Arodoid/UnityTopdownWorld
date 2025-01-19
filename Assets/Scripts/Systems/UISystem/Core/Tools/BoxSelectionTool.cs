using UnityEngine;
using Unity.Mathematics;
using UISystem.API;
using UISystem.Core.Interactions;
using WorldSystem.API;
using WorldSystem.Data;
using System.Threading.Tasks;

namespace UISystem.Core.Tools
{
    public class BoxSelectionTool : IUITool
    {
        private readonly WorldSystemAPI _worldAPI;
        private readonly WorldCoordinateMapper _coordinateMapper;
        private readonly BoxVisualizer _boxVisualizer;
        private BlockType _blockType = BlockType.Stone;
        private int3? _startPos;
        
        public string ToolId => "BoxTool";

        public BoxSelectionTool(WorldSystemAPI worldAPI, WorldCoordinateMapper coordinateMapper, GameObject toolsParent)
        {
            _worldAPI = worldAPI;
            _coordinateMapper = coordinateMapper;
            _boxVisualizer = toolsParent.AddComponent<BoxVisualizer>();
        }

        public void OnToolActivated()
        {
            _startPos = null;
            _boxVisualizer.Hide();
        }

        public void OnToolDeactivated()
        {
            _startPos = null;
            _boxVisualizer.Hide();
        }

        public void OnPointerDown(Vector2 position)
        {
            if (_coordinateMapper.TryGetHoveredBlockPosition(position, out int3 pos))
            {
                _startPos = new int3(pos.x, pos.y + 1, pos.z);
                _boxVisualizer.Show(_startPos.Value, _startPos.Value); // Show initial 1x1x1 box
            }
        }

        public void OnPointerDragged(Vector2 position)
        {
            if (_startPos.HasValue && _coordinateMapper.TryGetHoveredBlockPosition(position, out int3 currentPos))
            {
                int3 adjustedPos = new int3(currentPos.x, currentPos.y + 1, currentPos.z);
                _boxVisualizer.Show(_startPos.Value, adjustedPos);
            }
        }

        public async void OnPointerUp(Vector2 position)
        {
            if (_startPos.HasValue && _coordinateMapper.TryGetHoveredBlockPosition(position, out int3 endPos))
            {
                int3 adjustedEndPos = new int3(endPos.x, endPos.y + 1, endPos.z);
                await FillBox(_startPos.Value, adjustedEndPos);
                _boxVisualizer.Hide();
                _startPos = null;
            }
        }

        public void OnPointerMoved(Vector2 position) { }

        private async Task FillBox(int3 start, int3 end)
        {
            var (min, max) = _coordinateMapper.GetBoxBounds(start, end);
            
            for (int x = min.x; x <= max.x; x++)
                for (int y = min.y; y <= max.y; y++)
                    for (int z = min.z; z <= max.z; z++)
                        await _worldAPI.SetBlock(new int3(x, y, z), _blockType);
        }

        public void SetBlockType(BlockType blockType) => _blockType = blockType;

        public string GetSelectionInfo()
        {
            if (!_startPos.HasValue) return "Click and drag to create box";
            return "Release to create box";
        }
    }
} 