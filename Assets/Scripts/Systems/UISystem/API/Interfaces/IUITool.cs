using UnityEngine;

namespace UISystem.API
{
    public interface IUITool
    {
        string ToolId { get; }
        void OnToolActivated();
        void OnToolDeactivated();
        void OnPointerDown(Vector2 position);
        void OnPointerUp(Vector2 position);
        void OnPointerMoved(Vector2 position);
        void OnPointerDragged(Vector2 position);
    }
} 