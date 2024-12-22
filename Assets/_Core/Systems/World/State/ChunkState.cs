public enum ChunkState
{
    Unloaded,        // Chunk doesn't exist in memory
    Queued,          // In queue to be generated
    Generating,      // Data is being generated
    Generated,       // Data exists but no mesh
    Meshing,         // Mesh is being created
    Active,          // Fully loaded and visible
    Hidden,          // Loaded but not visible (covered by other chunks)
    PendingRemoval   // Marked for cleanup
} 