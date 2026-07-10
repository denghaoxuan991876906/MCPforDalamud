using System.Text.Json.Serialization;

namespace MCPforDalamud.Ipc;

public class IpcEndpoint
{
    [JsonPropertyName("pluginName")] public string PluginName { get; set; } = string.Empty;
    [JsonPropertyName("methodName")] public string MethodName { get; set; } = string.Empty;
    [JsonPropertyName("signature")] public string Signature { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
}

public class IpcEndpointRegistry
{
    private readonly List<IpcEndpoint> _endpoints = new();

    public void Register(string pluginName, string methodName, string signature, string description)
    {
        if (string.IsNullOrWhiteSpace(pluginName)) throw new ArgumentException("pluginName 不能为空");
        if (string.IsNullOrWhiteSpace(methodName)) throw new ArgumentException("methodName 不能为空");
        if (!IpcSignatures.Supported.Contains(signature)) throw new ArgumentException($"不支持的 IPC 签名: {signature}");
        if (pluginName.Any(char.IsWhiteSpace) || methodName.Any(char.IsWhiteSpace))
            throw new ArgumentException("插件名和方法名不能包含空白字符");
        var key = string.Format("{0}.{1}", pluginName, methodName);
        _endpoints.RemoveAll(e => string.Format("{0}.{1}", e.PluginName, e.MethodName) == key);
        _endpoints.Add(new IpcEndpoint { PluginName = pluginName, MethodName = methodName, Signature = signature, Description = description });
    }

    public List<IpcEndpoint> List(string? pluginName = null) => pluginName == null ? _endpoints.ToList() : _endpoints.Where(e => e.PluginName == pluginName).ToList();

    public IpcEndpoint? Find(string pluginName, string methodName) => _endpoints.FirstOrDefault(e => e.PluginName == pluginName && e.MethodName == methodName);
}
