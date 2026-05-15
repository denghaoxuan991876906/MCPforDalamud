using System.Runtime.InteropServices;
using System.Text;
using MCPforDalamud.McpServer;
using FFXIVClientStructs.FFXIV.Client.System.Framework;

namespace MCPforDalamud.Tools;

public static class ChatTools
{
    private delegate void ProcessChatBoxDelegate(IntPtr uiModule, IntPtr payload, IntPtr unused, byte a4);
    private static ProcessChatBoxDelegate? _processChatBox;
    private static readonly object _lock = new();

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    private struct ChatPayload : IDisposable
    {
        [FieldOffset(0)] private readonly IntPtr textPtr;
        [FieldOffset(8)] private readonly ulong unk1;
        [FieldOffset(16)] private readonly ulong textLen;
        [FieldOffset(24)] private readonly ulong unk2;
        public ChatPayload(byte[] stringBytes) { textPtr = Marshal.AllocHGlobal(stringBytes.Length + 30); Marshal.Copy(stringBytes, 0, textPtr, stringBytes.Length); Marshal.WriteByte(textPtr + stringBytes.Length, 0); textLen = (ulong)(stringBytes.Length + 1); unk1 = 64; unk2 = 0; }
        public void Dispose() { Marshal.FreeHGlobal(textPtr); }
    }

    public static void Register(ToolRegistry registry)
    {
        InitProcessChatBox();

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
        if (_processChatBox == null) return false;
        var bytes = Encoding.UTF8.GetBytes(message);
        if (bytes.Length > 500) return false;
        unsafe
        {
            var framework = Framework.Instance();
            var uiModule = framework->GetUIModule();
            if (uiModule == null) return false;
            using var payload = new ChatPayload(bytes);
            var mem = Marshal.AllocHGlobal(400);
            Marshal.StructureToPtr(payload, mem, false);
            _processChatBox((IntPtr)uiModule, mem, IntPtr.Zero, 0);
            Marshal.FreeHGlobal(mem);
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

    private static void InitProcessChatBox()
    {
        lock (_lock)
        {
            if (_processChatBox != null) return;
            try
            {
                var sig = "48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 48 8B F2 48 8B F9 45 84 C9";
                var addr = Service.SigScanner.ScanText(sig);
                if (addr != IntPtr.Zero)
                    _processChatBox = Marshal.GetDelegateForFunctionPointer<ProcessChatBoxDelegate>(addr);
            }
            catch { }
        }
    }
}
