using System.Text.Json;
using MCPforDalamud.Events;
using MCPforDalamud.McpServer;

namespace MCPforDalamud.Tools;

public static class EventTools
{
    private static EventCollector? _collector;
    public static void Initialize(EventCollector collector) { _collector = collector; }

    public static void Register(ToolRegistry registry)
    {
        registry.Register(new ToolDefinition { Name = "query_events", Description = "查询缓存的游戏事件", InputSchema = JsonSerializer.SerializeToElement(new { type = "object", properties = new { types = new { type = "array" }, count = new { type = "integer" }, since = new { type = "integer" }, before = new { type = "integer" } } }), Handler = args => { if (_collector == null) return new { error = "采集器未初始化" }; var types = args?.TryGetProperty("types", out var t) == true ? JsonSerializer.Deserialize<string[]>(t.GetRawText()) : null; var count = args?.TryGetProperty("count", out var c) == true ? c.GetInt32() : 50; if (count > 500) count = 500; var since = args?.TryGetProperty("since", out var s) == true ? (long?)s.GetInt64() : null; var before = args?.TryGetProperty("before", out var b) == true ? (long?)b.GetInt64() : null; var events = _collector.Buffer.Query(types, count, since, before); return new { events, total = _collector.Buffer.Count, config = _collector.Config }; } });
        registry.Register(new ToolDefinition { Name = "configure_event_collection", Description = "配置事件采集策略", InputSchema = JsonSerializer.SerializeToElement(new { type = "object", properties = new { config = new { type = "object" } }, required = new[] { "config" } }), Handler = args => { if (_collector == null) return new { error = "采集器未初始化" }; try { var configJson = args?.GetProperty("config").GetRawText() ?? "{}"; var newConfig = JsonSerializer.Deserialize<EventCollectionConfig>(configJson); if (newConfig != null) _collector.UpdateConfig(newConfig); return new { success = true, applied = _collector.Config }; } catch (Exception ex) { return new { success = false, error = ex.Message }; } } });
        registry.Register(new ToolDefinition { Name = "get_event_config", Description = "获取当前事件采集配置", Handler = _ => { if (_collector == null) return new { error = "采集器未初始化" }; return new { config = _collector.Config, bufferSize = _collector.Buffer.Count, maxBufferSize = _collector.Buffer.MaxSize }; } });
    }
}
