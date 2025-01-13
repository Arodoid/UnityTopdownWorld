using EntitySystem.Core.Types;

namespace EntitySystem.Core.Interfaces
{
    public interface IStateAwareComponent : IEntityComponent
    {
        void OnStateChanged(EntityState oldState, EntityState newState);
    }
} 