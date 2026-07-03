using System.Text;
using MCPforDalamud.McpServer;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace MCPforDalamud.Tools;

public static class ChatTools
{
    public static void Register(ToolRegistry registry)
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
        if (string.IsNullOrEmpty(pluginName)) return new { error = "请提供 pluginName" };
        var cmd = string.Format("/xlplugins disable {0}", pluginName);
        var ok = SendAsChat(cmd);
        return new { success = ok, action = "unload", pluginName };
    }

    private static object HandleLoad(string pluginName, string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return new { error = "请提供 filePath（插件DLL的完整路径）" };
        var cmd = string.Format("/xldev load \"{0}\"", filePath);
        var ok = SendAsChat(cmd);
        return new { success = ok, action = "load", filePath };
    }

    private static object HandleReload(string pluginName, string filePath)
    {
        if (!string.IsNullOrEmpty(pluginName))
        {
            SendAsChat(string.Format("/xlplugins disable {0}", pluginName));
            System.Threading.Thread.Sleep(500);
        }
        if (!string.IsNullOrEmpty(filePath))
        {
            var cmd = string.Format("/xldev load \"{0}\"", filePath);
            var ok = SendAsChat(cmd);
            return new { success = ok, action = "reload", pluginName, filePath };
        }
        return new { success = false, error = "请提供 filePath 或 pluginName" };
    }

    private static object HandleReloadAll()
    {
        var ok = SendAsChat("/xlplugins reload");
        return new { success = ok, action = "reload_all" };
    }

}
