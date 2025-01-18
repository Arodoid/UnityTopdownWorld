namespace EntitySystem.Core.Types
{
    public enum EntityState
    {
        Active,     // Entity is fully active and being updated
        Inactive,   // Entity exists but is not being updated
        Pooled      // Entity is in the object pool
    }
} 