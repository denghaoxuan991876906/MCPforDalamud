using System.Text;
using MCPforDalamud.McpServer;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace MCPforDalamud.Tools;

public static class ChatTools
{
    private static readonly CancellationTokenSource PendingOperations = new();

    public static void CancelPending() => PendingOperations.Cancel();

    public static void Register(ToolRegistry registry)
    {
        if (Plugin.Instance?.Config.AllowChat == true)
        {
        registry.Register(new ToolDefinition
        {
            Name = "send_chat",
            Description = "发送聊天消息。参数: message(内容), channel(say/yell/shout/party/alliance/fc/tell/echo)，默认say，target(tell目标名)",
            Handler = args =>
            {
                if (!Service.IsReady) throw new InvalidOperationException("游戏角色未登录");
                var message = args?.GetProperty("message").GetString() ?? "";
                var channel = args?.TryGetProperty("channel", out var ch) == true ? ch.GetString() ?? "say" : "say";
                var target = args?.TryGetProperty("target", out var tg) == true ? tg.GetString() ?? "" : "";
                if (string.IsNullOrEmpty(message)) return new { error = "请提供 message" };
                var prefix = channel.ToLower() switch
                {
                    "say" => "/s ", "yell" => "/y ", "shout" => "/sh ",
                    "party" => "/p ", "alliance" => "/a ", "fc" => "/fc ",
                    "tell" => string.Format("/tell {0} ", target), "echo" => "/e ",
                    _ => "/s "
                };
                return SendAsChat(prefix + message) switch
                {
                    true => (object)new { success = true, channel, message },
                    false => new { success = false, error = "聊天发送未初始化" }
                };
            }
        });
        }

        if (Plugin.Instance?.Config.AllowPluginManagement == true)
        {
        registry.Register(new ToolDefinition
        {
            Name = "manage_plugin",
            Description = "管理Dalamud插件：加载/卸载/重载。参数: action(load/unload/reload/all), pluginName(插件InternalName), filePath(仅load/reload时需要，DLL完整路径)",
            InputSchema = System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                type = "object",
                properties = new
                {
                    action = new { type = "string", description = "操作: load/unload/reload/all(全部重载)" },
                    pluginName = new { type = "string", description = "插件 InternalName，如 MCPforDalamud" },
                    filePath = new { type = "string", description = "仅load/reload时需要，插件DLL的完整路径" }
                },
                required = new[] { "action" }
            }),
            Handler = args =>
            {
                if (!Service.IsReady) throw new InvalidOperationException("游戏角色未登录");
                var action = args?.GetProperty("action").GetString()?.ToLower() ?? "";
                var pluginName = args?.TryGetProperty("pluginName", out var pn) == true ? pn.GetString() ?? "" : "";
                var filePath = args?.TryGetProperty("filePath", out var fp) == true ? fp.GetString() ?? "" : "";

                return action switch
                {
                    "unload" => HandleUnload(pluginName),
                    "load" => HandleLoad(pluginName, filePath),
                    "reload" => HandleReload(pluginName, filePath),
                    "all" => HandleReloadAll(),
                    _ => new { error = string.Format("未知操作: {0}，可选 load/unload/reload/all", action) }
                };
            }
        });
        }
    }

    private static bool SendAsChat(string message)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        if (bytes.Length > 500) return false;
        unsafe
        {
            var uiModule = UIModule.Instance();
            if (uiModule == null) return false;
            using var utf8String = new Utf8String(bytes);
            uiModule->ProcessChatBoxEntry(&utf8String);
        }
        return true;
    }

    private static object HandleUnload(string pluginName)
    {
        if (!IsValidPluginName(pluginName)) return new { error = "pluginName 只能包含字母、数字、点、下划线和连字符" };
        if (pluginName.Equals("MCPforDalamud", StringComparison.OrdinalIgnoreCase)) return new { error = "不能在当前 MCP 请求中卸载自身" };
        var cmd = string.Format("/xlplugins disable {0}", pluginName);
        var ok = SendAsChat(cmd);
        return new { success = ok, action = "unload", pluginName };
    }

    private static object HandleLoad(string pluginName, string filePath)
    {
        if (!TryValidateDllPath(filePath, out filePath, out var error)) return new { error };
        var cmd = string.Format("/xldev load \"{0}\"", filePath);
        var ok = SendAsChat(cmd);
        return new { success = ok, action = "load", filePath };
    }

    private static object HandleReload(string pluginName, string filePath)
    {
        if (!IsValidPluginName(pluginName)) return new { success = false, error = "reload 需要有效的 pluginName" };
        if (pluginName.Equals("MCPforDalamud", StringComparison.OrdinalIgnoreCase)) return new { success = false, error = "不能在当前 MCP 请求中重载自身" };
        if (!TryValidateDllPath(filePath, out filePath, out var error)) return new { success = false, error };
        if (!SendAsChat(string.Format("/xlplugins disable {0}", pluginName))) return new { success = false, error = "卸载命令发送失败" };
        ScheduleLoad(filePath);
        return new { success = true, scheduled = true, action = "reload", pluginName, filePath };
    }

    private static object HandleReloadAll()
    {
        var ok = SendAsChat("/xlplugins reload");
        return new { success = ok, action = "reload_all" };
    }

    internal static bool IsValidPluginName(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '.' or '_' or '-');

    internal static bool TryValidateDllPath(string value, out string fullPath, out string error)
    {
        fullPath = string.Empty;
        error = string.Empty;
        try
        {
            if (string.IsNullOrWhiteSpace(value) || value.IndexOfAny(new[] { '\r', '\n', '"' }) >= 0)
                throw new ArgumentException("filePath 无效");
            fullPath = Path.GetFullPath(value);
            if (!Path.GetExtension(fullPath).Equals(".dll", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("filePath 必须是 DLL 文件");
            if (!File.Exists(fullPath)) throw new FileNotFoundException("DLL 文件不存在", fullPath);
            return true;
        }
        catch (Exception ex) { error = ex.Message; return false; }
    }

    private static void ScheduleLoad(string filePath)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(500, PendingOperations.Token).ConfigureAwait(false);
                await Service.Framework.RunOnFrameworkThread(() => SendAsChat(string.Format("/xldev load \"{0}\"", filePath)));
            }
            catch (OperationCanceledException) when (PendingOperations.IsCancellationRequested) { }
            catch (Exception ex) { Service.PluginLog.Error(ex, "延迟加载插件失败"); }
        });
    }

}
