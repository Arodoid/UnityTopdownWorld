namespace EntitySystem.Core.Types
{
    public enum EntityState
    {
        Inactive,   // Entity exists but is not being updated
        Active,     // Entity is fully active and being updated
        Transitioning, // Entity is in the process of changing states
        Pooled      // Entity is in the object pool
    }
} 