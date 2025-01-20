using EntitySystem.Data;

namespace EntitySystem.Core.Components
{
    public interface IEntityComponent
    {
        void Initialize(Entity entity);
    }
} 