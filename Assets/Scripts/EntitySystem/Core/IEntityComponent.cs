namespace EntitySystem.Core
{
    public interface IEntityComponent
    {
        void Initialize(Entity entity);
        void OnTick();
        void OnDestroy();
    }
} 