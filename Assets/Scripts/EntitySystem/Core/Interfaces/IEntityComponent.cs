namespace EntitySystem.Core.Interfaces
{
    public interface IEntityComponent
    {
        void Initialize(Entity entity);
        void OnTick();
        void OnDestroy();
    }
} 