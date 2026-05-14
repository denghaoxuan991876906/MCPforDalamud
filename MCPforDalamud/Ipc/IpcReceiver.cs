using System.Text.Json;

namespace MCPforDalamud.Ipc;

public class IpcReceiver : IDisposable
{
    private readonly PushDataBuffer _buffer;
    private bool _disposed;

    public IpcReceiver(PushDataBuffer buffer)
    {
        _buffer = buffer;
        var provider = Service.PluginInterface.GetIpcProvider<string, object?>("MCPforDalamud.PushData");
        provider.RegisterFunc(OnPushData);
    }

    private object? OnPushData(string jsonData)
    {
        try
        {
            var doc = JsonDocument.Parse(jsonData);
            var key = doc.RootElement.TryGetProperty("key", out var k) ? k.GetString() ?? "" : "";
            var data = doc.RootElement.TryGetProperty("data", out var d) ? d.GetRawText() : jsonData;
            _buffer.Push(key, data);
        }
        catch { _buffer.Push("raw", jsonData); }
        return null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        var provider = Service.PluginInterface.GetIpcProvider<string, object?>("MCPforDalamud.PushData");
        provider.UnregisterFunc();
    }
}
