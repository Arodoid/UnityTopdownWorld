namespace EntitySystem.Core
{
    public interface IJob
    {
        void Start(Entity worker);
        bool Update();  // Returns true when job is complete
        void Cancel();
        bool CanAssignTo(Entity worker);
    }
} 