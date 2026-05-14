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
        registry.Register(new ToolDefinition { Name = "send_chat", Description = "发送聊天消息", Handler = args => { if (!Service.IsReady) throw new InvalidOperationException("游戏角色未登录"); var message = args?.GetProperty("message").GetString() ?? ""; var channel = args?.TryGetProperty("channel", out var ch) == true ? ch.GetString() ?? "say" : "say"; var target = args?.TryGetProperty("target", out var tg) == true ? tg.GetString() ?? "" : ""; if (string.IsNullOrEmpty(message)) return new { error = "请提供 message" }; var prefix = channel.ToLower() switch { "say" => "/s ", "yell" => "/y ", "shout" => "/sh ", "party" => "/p ", "alliance" => "/a ", "fc" => "/fc ", "tell" => string.Format("/tell {0} ", target), "echo" => "/e ", _ => "/s " }; var fullMessage = prefix + message; var bytes = Encoding.UTF8.GetBytes(fullMessage); if (bytes.Length > 500) return new { error = "消息过长" }; if (_processChatBox != null) { unsafe { var framework = Framework.Instance(); var uiModule = framework->GetUIModule(); if (uiModule == null) return new { error = "UIModule不可用" }; using var payload = new ChatPayload(bytes); var mem = Marshal.AllocHGlobal(400); Marshal.StructureToPtr(payload, mem, false); _processChatBox((IntPtr)uiModule, mem, IntPtr.Zero, 0); Marshal.FreeHGlobal(mem); } return new { success = true, channel, message }; } return new { success = false, error = "聊天发送未初始化" }; } });
    }

    private static void InitProcessChatBox() { lock (_lock) { if (_processChatBox != null) return; try { var sig = "48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 48 8B F2 48 8B F9 45 84 C9"; var addr = Service.SigScanner.ScanText(sig); if (addr != IntPtr.Zero) _processChatBox = Marshal.GetDelegateForFunctionPointer<ProcessChatBoxDelegate>(addr); } catch { } } }
}
