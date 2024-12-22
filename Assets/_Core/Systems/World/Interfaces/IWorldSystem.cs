/// <summary>
/// Base interface for all world-related systems.
/// Ensures consistent initialization and cleanup across systems.
/// </summary>
public interface IWorldSystem
{
    void Initialize();
    void Cleanup();
} 