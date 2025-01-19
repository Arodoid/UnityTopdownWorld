using UnityEngine;
using UISystem.API;

namespace UISystem.Core
{
    public class InputHandler : MonoBehaviour, IUIInteractionHandler
    {
        private Vector2 _lastPointerPosition;
        private bool _isPointerDown;
        private float _dragThreshold = 1f;
        private UISystemAPI _uiSystem;

        public void Initialize(UISystemAPI uiSystem)
        {
            _uiSystem = uiSystem;
        }

        private void Update()
        {
            if (_uiSystem == null) return;

            Vector2 currentPointerPosition = Input.mousePosition;

            // Check if mouse is within the game window
            if (!IsMouseInGameWindow(currentPointerPosition))
            {
                return;
            }

            // Always handle pointer movement, even if position hasn't changed
            HandlePointerMoved(currentPointerPosition);
            
            // Handle dragging if any mouse button is held down
            if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
            {
                _isPointerDown = true;
                HandlePointerDragged(currentPointerPosition);
            }

            // Handle mouse button down events
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
            {
                HandlePointerDown(currentPointerPosition);
            }

            // Handle mouse button up events
            if (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1))
            {
                _isPointerDown = false;
                HandlePointerUp(currentPointerPosition);
            }

            _lastPointerPosition = currentPointerPosition;
        }

        private bool IsMouseInGameWindow(Vector2 mousePosition)
        {
            return mousePosition.x >= 0 && mousePosition.x < Screen.width &&
                   mousePosition.y >= 0 && mousePosition.y < Screen.height;
        }

        public bool IsPointerOverUI()
        {
            return UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
        }

        public Vector2 GetPointerPosition()
        {
            return _lastPointerPosition;
        }

        public void HandlePointerDown(Vector2 position)
        {
            (_uiSystem as IInputHandler)?.HandleInput(position, InputType.Down);
        }

        public void HandlePointerUp(Vector2 position)
        {
            (_uiSystem as IInputHandler)?.HandleInput(position, InputType.Up);
        }

        public void HandlePointerMoved(Vector2 position)
        {
            (_uiSystem as IInputHandler)?.HandleInput(position, InputType.Move);
        }

        private void HandlePointerDragged(Vector2 position)
        {
            (_uiSystem as IInputHandler)?.HandleInput(position, InputType.Drag);
        }
    }
} 