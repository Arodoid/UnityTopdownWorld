using UnityEngine;
using Unity.Mathematics;
using System;
using WorldSystem.API;
using UISystem.Core.Interactions;

namespace UISystem.Core.Selection
{
    public class SingleBlockSelectionResult
    {
        public int3 Position { get; }
        public bool IsValid { get; }

        public SingleBlockSelectionResult(int3 position, bool isValid)
        {
            Position = position;
            IsValid = isValid;
        }
    }

    public class SingleBlockSelector
    {
        private readonly WorldCoordinateMapper _coordinateMapper;
        private readonly BlockHighlighter _blockHighlighter;
        private SingleBlockSelectionResult _currentSelection;

        public event Action<SingleBlockSelectionResult> OnSelectionChanged;

        public SingleBlockSelector(WorldCoordinateMapper coordinateMapper, BlockHighlighter blockHighlighter)
        {
            _coordinateMapper = coordinateMapper;
            _blockHighlighter = blockHighlighter;
        }

        public void ShowSelection()
        {
            _blockHighlighter.ShowHighlight();
        }

        public void HideSelection()
        {
            _blockHighlighter.HideHighlight();
            _currentSelection = null;
        }

        public void UpdateSelection(Vector2 screenPosition)
        {
            _blockHighlighter.UpdateHighlight(screenPosition);
            
            if (_blockHighlighter.GetHighlightedPosition() is int3 blockPos)
            {
                var newSelection = new SingleBlockSelectionResult(blockPos, true);
                
                // Only trigger event if selection changed
                if (_currentSelection == null || !_currentSelection.Position.Equals(blockPos))
                {
                    _currentSelection = newSelection;
                    OnSelectionChanged?.Invoke(newSelection);
                }
            }
            else if (_currentSelection != null)
            {
                _currentSelection = null;
                OnSelectionChanged?.Invoke(new SingleBlockSelectionResult(default, false));
            }
        }

        public SingleBlockSelectionResult GetCurrentSelection() => _currentSelection;
    }
} 