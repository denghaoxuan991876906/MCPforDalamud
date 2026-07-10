using System.Text.Json;
using Dalamud.Plugin;

namespace MCPforDalamud.Ipc;

public interface IPluginIpcInvoker
{
    object? Invoke(string ipcName, string signature, JsonElement? arguments);
}

public sealed class DalamudPluginIpcInvoker : IPluginIpcInvoker
{
    private readonly IDalamudPluginInterface _pluginInterface;

    public DalamudPluginIpcInvoker(IDalamudPluginInterface pluginInterface)
    {
        _pluginInterface = pluginInterface;
    }

    public object? Invoke(string ipcName, string signature, JsonElement? arguments)
    {
        return signature switch
        {
            IpcSignatures.FuncBool => InvokeFunc<bool>(ipcName, arguments),
            IpcSignatures.FuncString => InvokeFunc<string>(ipcName, arguments),
            IpcSignatures.FuncIntString => _pluginInterface.GetIpcSubscriber<int, string>(ipcName)
                .InvokeFunc(ReadValue<int>(arguments)),
            IpcSignatures.ActionBool => InvokeAction(ipcName, ReadValue<bool>(arguments)),
            IpcSignatures.FuncBoolString => _pluginInterface.GetIpcSubscriber<bool, string>(ipcName)
                .InvokeFunc(ReadValue<bool>(arguments)),
            _ => throw new NotSupportedException($"不支持的 IPC 签名: {signature}")
        };
    }

    private T InvokeFunc<T>(string ipcName, JsonElement? arguments)
    {
        EnsureNoArguments(arguments);
        return _pluginInterface.GetIpcSubscriber<T>(ipcName).InvokeFunc();
    }

    private string InvokeAction(string ipcName, bool value)
    {
        _pluginInterface.GetIpcSubscriber<bool, object?>(ipcName).InvokeAction(value);
        return "invoked";
    }

    internal static T ReadValue<T>(JsonElement? arguments)
    {
        if (arguments is not { ValueKind: JsonValueKind.Object } ||
            !arguments.Value.TryGetProperty("value", out var value))
            throw new ArgumentException("该 IPC 签名需要 arguments.value");

        try
        {
            return value.Deserialize<T>()!;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new ArgumentException($"arguments.value 无法转换为 {typeof(T).Name}", ex);
        }
    }

    private static void EnsureNoArguments(JsonElement? arguments)
    {
        if (arguments is { ValueKind: JsonValueKind.Object } && arguments.Value.EnumerateObject().Any())
            throw new ArgumentException("该 IPC 签名不接受参数");
    }
}

public static class IpcSignatures
{
    public const string FuncBool = "Func_bool";
    public const string FuncString = "Func_string";
    public const string FuncIntString = "Func_int_string";
    public const string ActionBool = "Action_bool";
    public const string FuncBoolString = "Func_bool_string";

    public static readonly IReadOnlySet<string> Supported = new HashSet<string>(StringComparer.Ordinal)
    {
        FuncBool, FuncString, FuncIntString, ActionBool, FuncBoolString
    };
}
