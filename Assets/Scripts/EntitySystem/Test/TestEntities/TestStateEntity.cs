using UnityEngine;
using EntitySystem.Core;
using EntitySystem.Core.Interfaces;
using EntitySystem.Core.Types;

namespace EntitySystem.Test.TestEntities
{
    public class TestStateComponent : EntityComponent
    {
        private bool _hasLoggedState;

        public override void OnStateChanged(EntityState oldState, EntityState newState)
        {
            _hasLoggedState = true;
            Debug.Log($"State changed from {oldState} to {newState}");
        }

        public bool HasLoggedStateChange() => _hasLoggedState;
    }

    public class TestPositionComponent : EntityComponent
    {
        private bool _hasLoggedPosition;

        public override void OnPositionChanged(Vector3 oldPosition, Vector3 newPosition)
        {
            _hasLoggedPosition = true;
            Debug.Log($"Position changed from {oldPosition} to {newPosition}");
        }

        public bool HasLoggedPositionChange() => _hasLoggedPosition;
    }

    public class TestStateEntity : Entity
    {
        public TestStateEntity(long id, EntityManager manager) : base(id, manager) { }

        protected override void SetupComponents()
        {
            AddComponent<TestStateComponent>();
            AddComponent<TestPositionComponent>();
        }

        public bool HasLoggedStateChange()
        {
            var component = GetComponent<TestStateComponent>();
            return component != null && component.HasLoggedStateChange();
        }

        public bool HasLoggedPositionChange()
        {
            var component = GetComponent<TestPositionComponent>();
            return component != null && component.HasLoggedPositionChange();
        }
    }
} 