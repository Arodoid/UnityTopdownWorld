using UnityEngine;
using Unity.Mathematics;
using UISystem.API;
using UISystem.Core.Interactions;
using EntitySystem.API;
using System.Collections.Generic;
using System.Linq;

namespace UISystem.Core.Tools
{
    public class EntitySpawnTool : IUITool
    {
        private readonly EntitySystemAPI _entityAPI;
        private readonly WorldCoordinateMapper _coordinateMapper;
        private string _selectedEntityId;
        private List<string> _availableEntities;

        public string ToolId => "EntitySpawnTool";
        public string SelectedEntityId => _selectedEntityId;
        public IReadOnlyList<string> AvailableEntities => _availableEntities;

        public EntitySpawnTool(EntitySystemAPI entityAPI, WorldCoordinateMapper coordinateMapper)
        {
            _entityAPI = entityAPI;
            _coordinateMapper = coordinateMapper;
            _availableEntities = _entityAPI.GetAvailableEntityTemplates().ToList();
            _selectedEntityId = _availableEntities.FirstOrDefault();
        }

        public void OnToolActivated()
        {
        }

        public void OnToolDeactivated() { }

        public void OnPointerDown(Vector2 screenPosition)
        {
            if (string.IsNullOrEmpty(_selectedEntityId)) return;

            if (_coordinateMapper.TryGetHoveredBlockPosition(screenPosition, out int3 blockPosition))
            {
                // Convert block position to world position (centered in block)
                var worldPosition = new int3(
                    blockPosition.x,
                    blockPosition.y,
                    blockPosition.z
                );
                
                var handle = _entityAPI.CreateEntity(_selectedEntityId, worldPosition);
            }
        }

        public void OnPointerUp(Vector2 position) { }
        public void OnPointerMoved(Vector2 position) { }
        public void OnPointerDragged(Vector2 position) { }

        public void SetSelectedEntity(string entityId)
        {
            if (_availableEntities.Contains(entityId))
            {
                _selectedEntityId = entityId;
            }
        }

        public string GetToolInfo()
        {
            return $"Spawning: {_selectedEntityId}\nClick to spawn entity";
        }
    }
} 