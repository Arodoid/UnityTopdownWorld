namespace EntitySystem.Core
{
    public interface IJob
    {
        void Start(Entity worker);
        bool Update();  // Returns true when job is complete
        void Cancel();
        bool CanAssignTo(Entity worker);
        
        // New method to help job system prioritize jobs of the same type
        float GetPriority(Entity worker);
        bool IsComplete { get; }  // New property to check if job actually completed its task
    }
} 