using UnityEngine;
using System;
using UISystem.Core;

namespace UISystem.API
{
    internal interface IInputHandler
    {
        void HandleInput(Vector2 position, InputType inputType);
    }

    public class UISystemAPI : IInputHandler
    {
        private readonly IUIInteractionHandler _interactionHandler;
        private readonly IToolManager _toolManager;
        
        // Events that other systems can subscribe to
        public event Action<Vector2> OnPointerMoved;
        public event Action<Vector2> OnPointerDown;
        public event Action<Vector2> OnPointerUp;
        public event Action<Vector2> OnPointerDragged;
        
        public UISystemAPI(IUIInteractionHandler interactionHandler, IToolManager toolManager)
        {
            _interactionHandler = interactionHandler;
            _toolManager = toolManager;

            // Initialize the input handler
            if (_interactionHandler is MonoBehaviour handler)
            {
                var inputHandler = handler as InputHandler;
                inputHandler?.Initialize(this);
            }
        }

        // Core methods for tool management
        public void SetActiveTool(string toolId)
        {
            _toolManager.SetActiveTool(toolId);
        }

        public string GetActiveTool()
        {
            return _toolManager.GetActiveToolId();
        }

        // Input handling methods that tools will use
        public bool IsPointerOverUI()
        {
            return _interactionHandler.IsPointerOverUI();
        }

        public Vector2 GetPointerPosition()
        {
            return _interactionHandler.GetPointerPosition();
        }

        void IInputHandler.HandleInput(Vector2 position, InputType inputType)
        {
            // Get the active tool
            string activeToolId = _toolManager.GetActiveToolId();
            if (string.IsNullOrEmpty(activeToolId)) return;

            // Route input to the active tool
            if (_toolManager.IsToolActive(activeToolId))
            {
                var activeTool = _toolManager.GetTool(activeToolId);
                if (activeTool != null)
                {
                    switch (inputType)
                    {
                        case InputType.Move:
                            OnPointerMoved?.Invoke(position);
                            activeTool.OnPointerMoved(position);
                            break;
                        case InputType.Down:
                            OnPointerDown?.Invoke(position);
                            activeTool.OnPointerDown(position);
                            break;
                        case InputType.Up:
                            OnPointerUp?.Invoke(position);
                            activeTool.OnPointerUp(position);
                            break;
                        case InputType.Drag:
                            OnPointerDragged?.Invoke(position);
                            activeTool.OnPointerDragged(position);
                            break;
                    }
                }
            }
        }

        public IUITool GetTool(string toolId)
        {
            return _toolManager.GetTool(toolId);
        }
    }

    internal enum InputType
    {
        Move,
        Down,
        Up,
        Drag
    }
} 