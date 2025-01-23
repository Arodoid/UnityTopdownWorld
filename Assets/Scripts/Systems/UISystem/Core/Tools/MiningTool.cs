using UnityEngine;
using Unity.Mathematics;
using UISystem.API;
using UISystem.Core.Selection;
using UISystem.Core.Interactions;
using EntitySystem.API;
using EntitySystem.Core;
using EntitySystem.Core.Jobs;
using WorldSystem.API;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace UISystem.Core.Tools
{
    public class MiningTool : IUITool
    {
        private readonly WorldSystemAPI _worldAPI;
        private readonly EntitySystemAPI _entityAPI;
        private readonly BoxSelector _boxSelector;
        
        public string ToolId => "MiningTool";

        public MiningTool(WorldSystemAPI worldAPI, EntitySystemAPI entityAPI, 
            WorldCoordinateMapper coordinateMapper, GameObject toolsParent)
        {
            _worldAPI = worldAPI;
            _entityAPI = entityAPI;
            
            // Create box visualizer for selection
            var visualizerObject = new GameObject("MiningVisualizer");
            visualizerObject.transform.SetParent(toolsParent.transform);
            var boxVisualizer = visualizerObject.AddComponent<BoxVisualizer>();
            
            _boxSelector = new BoxSelector(coordinateMapper, boxVisualizer);
            _boxSelector.OnSelectionFinished += HandleSelectionFinished;
        }

        public void OnToolActivated()
        {
            Debug.Log("Mining tool activated");
        }

        public void OnToolDeactivated()
        {
            _boxSelector.CancelSelection();
        }

        public void OnPointerDown(Vector2 position)
        {
            _boxSelector.StartSelection(position);
        }

        public void OnPointerUp(Vector2 position)
        {
            _boxSelector.FinishSelection(position);
        }

        public void OnPointerMoved(Vector2 position)
        {
            _boxSelector.UpdateSelection(position);
        }

        public void OnPointerDragged(Vector2 position)
        {
            _boxSelector.UpdateSelection(position);
        }

        private async void HandleSelectionFinished(BoxSelectionResult selection)
        {
            if (!selection.IsValid) return;

            var (min, max) = GetBoxBounds(selection.Start, selection.End);
            Debug.Log($"Processing mining selection from {min} to {max}");
            
            // First gather all block positions and their types
            var blockChecks = new List<(int3 position, Task<WorldSystem.Data.BlockType> typeTask)>();
            
            // Start all block type checks
            for (int x = min.x; x <= max.x; x++)
            {
                for (int y = min.y; y <= max.y; y++)
                {
                    for (int z = min.z; z <= max.z; z++)
                    {
                        var pos = new int3(x, y, z);
                        blockChecks.Add((pos, _worldAPI.GetBlockType(pos)));
                    }
                }
            }

            // Wait for all block checks to complete
            try 
            {
                foreach (var (pos, typeTask) in blockChecks)
                {
                    var blockType = await typeTask;
                    if (blockType != WorldSystem.Data.BlockType.Air)
                    {
                        var mineJob = new MineBlockJob(pos, _worldAPI);
                        _entityAPI.AddGlobalJob(mineJob);
                    }
                }
                
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error processing mining selection: {e.Message}");
            }
        }

        private (int3 min, int3 max) GetBoxBounds(int3 start, int3 end)
        {
            return (
                new int3(
                    Mathf.Min(start.x, end.x),
                    Mathf.Min(start.y, end.y),
                    Mathf.Min(start.z, end.z)
                ),
                new int3(
                    Mathf.Max(start.x, end.x),
                    Mathf.Max(start.y, end.y),
                    Mathf.Max(start.z, end.z)
                )
            );
        }

        public string GetToolInfo()
        {
            return "Click and drag to select blocks to mine";
        }
    }
} 