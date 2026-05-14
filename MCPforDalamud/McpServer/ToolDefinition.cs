using System.Text.Json;

namespace MCPforDalamud.McpServer;

public class ToolDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public JsonElement? InputSchema { get; set; }
    public Func<JsonElement?, object?> Handler { get; set; } = _ => null;
}
