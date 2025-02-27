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

        public BoxSelectionResult(int3 start, int3 end)
        {
            Start = start;
            End = end;
            Debug.Log($"BoxSelectionResult: Created with start={start}, end={end}");
            IsValid = ValidateSelection(start, end);
            Debug.Log($"BoxSelectionResult: IsValid={IsValid}");
        }

        private bool ValidateSelection(int3 start, int3 end)
        {
            if (start.Equals(end))
            {
                Debug.Log("BoxSelectionResult: Invalid - start equals end");
                return false;
            }

            return true;
        }
    }

    public class BoxSelector
    {
        private readonly WorldCoordinateMapper _coordinateMapper;
        private readonly BoxVisualizer _boxVisualizer;
        private int3? _startPos;

        public event Action<BoxSelectionResult> OnSelectionFinished;

        public BoxSelector(WorldCoordinateMapper coordinateMapper, BoxVisualizer boxVisualizer)
        {
            _coordinateMapper = coordinateMapper;
            _boxVisualizer = boxVisualizer;
        }

        public void StartSelection(Vector2 screenPosition)
        {
            if (_coordinateMapper.TryGetHoveredBlockPosition(screenPosition, out int3 pos))
            {
                _startPos = pos;
                _boxVisualizer.Show(_startPos.Value, _startPos.Value);
            }
        }

        public void UpdateSelection(Vector2 screenPosition)
        {
            if (_startPos.HasValue && _coordinateMapper.TryGetHoveredBlockPosition(screenPosition, out int3 currentPos))
            {
                _boxVisualizer.Show(_startPos.Value, currentPos);
            }
        }

        public void FinishSelection(Vector2 screenPosition)
        {
            if (_startPos.HasValue && _coordinateMapper.TryGetHoveredBlockPosition(screenPosition, out int3 endPos))
            {
                var finalSelection = new BoxSelectionResult(_startPos.Value, endPos);
                _boxVisualizer.Hide();
                _startPos = null;
                OnSelectionFinished?.Invoke(finalSelection);
            }
        }

        public void CancelSelection()
        {
            _startPos = null;
            _boxVisualizer.Hide();
        }
    }
} 