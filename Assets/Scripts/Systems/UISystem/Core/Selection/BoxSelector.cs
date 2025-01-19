using UnityEngine;
using Unity.Mathematics;
using System;
using UISystem.Core.Interactions;

namespace UISystem.Core.Selection
{
    public class BoxSelectionResult
    {
        public int3 Start { get; }
        public int3 End { get; }
        public bool IsValid { get; }

        public BoxSelectionResult(int3 start, int3 end, bool isValid)
        {
            Start = start;
            End = end;
            IsValid = isValid;
        }
    }

    public class BoxSelector
    {
        private readonly WorldCoordinateMapper _coordinateMapper;
        private readonly BoxVisualizer _boxVisualizer;
        private int3? _startPos;
        private BoxSelectionResult _currentSelection;

        public event Action<BoxSelectionResult> OnSelectionChanged;
        public event Action OnSelectionStarted;
        public event Action OnSelectionFinished;

        public BoxSelector(WorldCoordinateMapper coordinateMapper, BoxVisualizer boxVisualizer)
        {
            _coordinateMapper = coordinateMapper;
            _boxVisualizer = boxVisualizer;
        }

        public void StartSelection(Vector2 screenPosition)
        {
            if (_coordinateMapper.TryGetHoveredBlockPosition(screenPosition, out int3 pos))
            {
                _startPos = new int3(pos.x, pos.y + 1, pos.z);
                _boxVisualizer.Show(_startPos.Value, _startPos.Value);
                OnSelectionStarted?.Invoke();
            }
        }

        public void UpdateSelection(Vector2 screenPosition)
        {
            if (_startPos.HasValue && _coordinateMapper.TryGetHoveredBlockPosition(screenPosition, out int3 currentPos))
            {
                int3 adjustedPos = new int3(currentPos.x, currentPos.y + 1, currentPos.z);
                _boxVisualizer.Show(_startPos.Value, adjustedPos);
                
                var newSelection = new BoxSelectionResult(_startPos.Value, adjustedPos, true);
                if (!_currentSelection?.Start.Equals(newSelection.Start) == true || 
                    !_currentSelection?.End.Equals(newSelection.End) == true)
                {
                    _currentSelection = newSelection;
                    OnSelectionChanged?.Invoke(newSelection);
                }
            }
        }

        public BoxSelectionResult FinishSelection(Vector2 screenPosition)
        {
            if (_startPos.HasValue && _coordinateMapper.TryGetHoveredBlockPosition(screenPosition, out int3 endPos))
            {
                int3 adjustedEndPos = new int3(endPos.x, endPos.y + 1, endPos.z);
                var finalSelection = new BoxSelectionResult(_startPos.Value, adjustedEndPos, true);
                _boxVisualizer.Hide();
                _startPos = null;
                _currentSelection = null;
                OnSelectionFinished?.Invoke();
                return finalSelection;
            }
            return new BoxSelectionResult(default, default, false);
        }

        public void CancelSelection()
        {
            _startPos = null;
            _currentSelection = null;
            _boxVisualizer.Hide();
        }

        public BoxSelectionResult GetCurrentSelection() => _currentSelection;
    }
} 