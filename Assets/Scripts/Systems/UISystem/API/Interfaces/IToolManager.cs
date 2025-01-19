namespace UISystem.API
{
    public interface IToolManager
    {
        void SetActiveTool(string toolId);
        string GetActiveToolId();
        bool IsToolActive(string toolId);
        void RegisterTool(IUITool tool);
        void UnregisterTool(string toolId);
    }
} 