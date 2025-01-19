using UnityEngine;
using System;

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

            // If the interaction handler is a MonoBehaviour, initialize it
            if (_interactionHandler is MonoBehaviour handler)
            {
                var inputHandler = handler as Core.InputHandler;
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
            switch (inputType)
            {
                case InputType.Move:
                    OnPointerMoved?.Invoke(position);
                    break;
                case InputType.Down:
                    OnPointerDown?.Invoke(position);
                    break;
                case InputType.Up:
                    OnPointerUp?.Invoke(position);
                    break;
                case InputType.Drag:
                    OnPointerDragged?.Invoke(position);
                    break;
            }
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