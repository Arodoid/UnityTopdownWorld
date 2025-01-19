using UnityEngine;

namespace UISystem.API
{
    public interface IUIInteractionHandler
    {
        bool IsPointerOverUI();
        Vector2 GetPointerPosition();
        void HandlePointerDown(Vector2 position);
        void HandlePointerUp(Vector2 position);
        void HandlePointerMoved(Vector2 position);
    }
} 