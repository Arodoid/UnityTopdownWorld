using UnityEngine;
using UISystem.API;

namespace UISystem.Core
{
    public class InputHandler : MonoBehaviour, IUIInteractionHandler
    {
        private Vector2 _lastPointerPosition;
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

            // Check if pointer is over UI - if so, only handle UI interactions
            bool isOverUI = IsPointerOverUI();
            
            // Always handle pointer movement if not over UI
            if (!isOverUI)
            {
                HandlePointerMoved(currentPointerPosition);
            }
            
            // Handle dragging if any mouse button is held down and not over UI
            if (!isOverUI && (Input.GetMouseButton(0) || Input.GetMouseButton(1)))
            {
                HandlePointerDragged(currentPointerPosition);
            }

            // Handle mouse button down events if not over UI
            if (!isOverUI && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)))
            {
                HandlePointerDown(currentPointerPosition);
            }

            // Handle mouse button up events if not over UI
            if (!isOverUI && (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1)))
            {
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
            var eventSystem = UnityEngine.EventSystems.EventSystem.current;
            if (eventSystem == null) return false;
            return eventSystem.IsPointerOverGameObject();
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