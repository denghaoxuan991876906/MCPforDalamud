using System.Text.Json;
using MCPforDalamud.Ipc;
using MCPforDalamud.McpServer;

namespace MCPforDalamud.Tools;

public static class PluginBridgeTools
{
    private static PushDataBuffer? _pushBuffer;
    private static IpcEndpointRegistry? _endpointRegistry;
    private static IPluginIpcInvoker? _ipcInvoker;
    public static void Initialize(PushDataBuffer pushBuffer, IpcEndpointRegistry endpointRegistry)
        => Initialize(pushBuffer, endpointRegistry, new DalamudPluginIpcInvoker(Service.PluginInterface));

    public static void Initialize(PushDataBuffer pushBuffer, IpcEndpointRegistry endpointRegistry, IPluginIpcInvoker ipcInvoker)
    {
        _pushBuffer = pushBuffer;
        _endpointRegistry = endpointRegistry;
        _ipcInvoker = ipcInvoker;
    }

    public static void Reset()
    {
        _pushBuffer = null;
        _endpointRegistry = null;
        _ipcInvoker = null;
    }

    public static void Register(ToolRegistry registry)
    {
        registry.Register(new ToolDefinition { Name = "query_push_data", Description = "查询其他插件推送的数据缓存", InputSchema = JsonSerializer.SerializeToElement(new { type = "object", properties = new { key = new { type = "string" }, count = new { type = "integer" } } }), Handler = args => { if (_pushBuffer == null) return new { error = "PushData 缓冲区未初始化" }; var key = args?.TryGetProperty("key", out var k) == true ? k.GetString() : null; var count = args?.TryGetProperty("count", out var c) == true ? c.GetInt32() : 50; var entries = _pushBuffer.Query(key, count); return new { entries, total = _pushBuffer.Count }; } });
        registry.Register(new ToolDefinition { Name = "register_ipc_endpoint", Description = "注册已知的插件 IPC 接口", InputSchema = JsonSerializer.SerializeToElement(new { type = "object", properties = new { pluginName = new { type = "string", minLength = 1 }, methodName = new { type = "string", minLength = 1 }, signature = new { type = "string", @enum = IpcSignatures.Supported.ToArray() }, description = new { type = "string" } }, required = new[] { "pluginName", "methodName", "signature" } }), Handler = args => { if (_endpointRegistry == null) return new { success = false, error = "端点注册表未初始化" }; try { var pn = args?.GetProperty("pluginName").GetString() ?? ""; var mn = args?.GetProperty("methodName").GetString() ?? ""; var sig = args?.GetProperty("signature").GetString() ?? ""; var desc = args?.TryGetProperty("description", out var d) == true ? d.GetString() ?? "" : ""; _endpointRegistry.Register(pn, mn, sig, desc); if (Plugin.Instance != null) { Plugin.Instance.Config.IpcEndpoints = _endpointRegistry.List(); Plugin.Instance.Config.Save(); } return new { success = true, error = (string?)null }; } catch (Exception ex) { return new { success = false, error = ex.Message }; } } });
        registry.Register(new ToolDefinition { Name = "call_plugin_ipc", Description = "调用已注册的插件 IPC 接口。单参数签名使用 arguments.value", InputSchema = JsonSerializer.SerializeToElement(new { type = "object", properties = new { pluginName = new { type = "string", minLength = 1 }, methodName = new { type = "string", minLength = 1 }, arguments = new { type = "object", properties = new { value = new { } } } }, required = new[] { "pluginName", "methodName" } }), Handler = args => { if (_endpointRegistry == null || _ipcInvoker == null) return new { success = false, pluginName = "", methodName = "", result = (object?)null, error = "IPC 桥接未初始化" }; var pn = args?.GetProperty("pluginName").GetString() ?? ""; var mn = args?.GetProperty("methodName").GetString() ?? ""; var endpoint = _endpointRegistry.Find(pn, mn); if (endpoint == null) return new { success = false, pluginName = pn, methodName = mn, result = (object?)null, error = string.Format("未注册的端点: {0}.{1}", pn, mn) }; try { JsonElement? ipcArguments = args?.TryGetProperty("arguments", out var argElem) == true ? argElem.Clone() : null; var result = _ipcInvoker.Invoke(string.Format("{0}.{1}", pn, mn), endpoint.Signature, ipcArguments); return new { success = true, pluginName = pn, methodName = mn, result, error = (string?)null }; } catch (Exception ex) { return new { success = false, pluginName = pn, methodName = mn, result = (object?)null, error = ex.Message }; } } });
        registry.Register(new ToolDefinition { Name = "list_ipc_endpoints", Description = "列出已注册的 IPC 端点", InputSchema = JsonSerializer.SerializeToElement(new { type = "object", properties = new { pluginName = new { type = "string" } } }), Handler = args => { if (_endpointRegistry == null) return new { error = "端点注册表未初始化" }; var pn = args?.TryGetProperty("pluginName", out var p) == true ? p.GetString() : null; var endpoints = _endpointRegistry.List(pn); return new { count = endpoints.Count, endpoints }; } });
    }
}
