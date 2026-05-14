using System.Text.Json;

namespace MCPforDalamud.McpServer;

public class ToolRegistry
{
    private readonly Dictionary<string, ToolDefinition> _tools = new();
    private readonly IFrameworkRunner _runner;

    public ToolRegistry(IFrameworkRunner runner)
    {
        _runner = runner;
    }

    public void Register(ToolDefinition tool)
    {
        if (_tools.ContainsKey(tool.Name))
            throw new InvalidOperationException(string.Format("工具 {0} 已注册", tool.Name));
        _tools[tool.Name] = tool;
    }

    public List<ToolDefinition> ListTools()
    {
        return _tools.Values.ToList();
    }

    public object? CallTool(string name, JsonElement? arguments)
    {
        if (!_tools.TryGetValue(name, out var tool))
            throw new InvalidOperationException(string.Format("未找到工具: {0}", name));

        object? result = null;
        Exception? capturedException = null;

        _runner.Run(() =>
        {
            try
            {
                result = tool.Handler(arguments);
            }
            catch (Exception ex)
            {
                capturedException = ex;
            }
        });

        if (capturedException != null)
            throw capturedException;

        return result;
    }
}
