using UnityEngine;
using UISystem.API;

namespace UISystem.Core
{
    public class InputHandler : MonoBehaviour, IUIInteractionHandler
    {
        private Vector2 _lastPointerPosition;
        private bool _isPointerDown;
        private float _dragThreshold = 3f;
        private UISystemAPI _uiSystem;

        public void Initialize(UISystemAPI uiSystem)
        {
            _uiSystem = uiSystem;
        }

        private void Update()
        {
            if (_uiSystem == null) return;

            // Get the current mouse position
            Vector2 mousePosition = Input.mousePosition;

            // Check if mouse is within the game window
            if (!IsMouseInGameWindow(mousePosition))
            {
                return;
            }

            Vector2 currentPointerPosition = Input.mousePosition;

            // Handle pointer movement
            if (currentPointerPosition != _lastPointerPosition)
            {
                HandlePointerMoved(currentPointerPosition);
                
                // Handle dragging if pointer is down
                if (_isPointerDown)
                {
                    float dragDistance = Vector2.Distance(_lastPointerPosition, currentPointerPosition);
                    if (dragDistance > _dragThreshold)
                    {
                        HandlePointerDragged(currentPointerPosition);
                    }
                }
            }

            // Handle left mouse button (0)
            if (Input.GetMouseButtonDown(0))
            {
                HandlePointerDown(currentPointerPosition);
            }
            else if (Input.GetMouseButtonUp(0))
            {
                HandlePointerUp(currentPointerPosition);
            }
            
            // Handle right mouse button (1)
            if (Input.GetMouseButtonDown(1))
            {
                HandlePointerDown(currentPointerPosition);
            }
            else if (Input.GetMouseButtonUp(1))
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
            return UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
        }

        public Vector2 GetPointerPosition()
        {
            return _lastPointerPosition;
        }

        public void HandlePointerDown(Vector2 position)
        {
            _isPointerDown = true;
            (_uiSystem as IInputHandler)?.HandleInput(position, InputType.Down);
        }

        public void HandlePointerUp(Vector2 position)
        {
            _isPointerDown = false;
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