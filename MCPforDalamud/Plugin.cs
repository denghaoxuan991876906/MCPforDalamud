using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using MCPforDalamud.Events;
using MCPforDalamud.Ipc;
using MCPforDalamud.McpServer;
using MCPforDalamud.Tools;
using MCPforDalamud.UI;

namespace MCPforDalamud;

public class Plugin : IDalamudPlugin
{
    public string Name => "MCP for Dalamud";

    public static Plugin? Instance { get; private set; }
    public Configuration Config { get; }

    private McpHttpServer? _httpServer;
    private ToolRegistry? _registry;
    private WindowSystem? _windowSystem;
    private ConfigWindow? _configWindow;
    private EventCollector? _eventCollector;
    private IpcReceiver? _ipcReceiver;
    private PushDataBuffer? _pushDataBuffer;
    private IpcEndpointRegistry? _ipcEndpointRegistry;
    private Action? _drawUi;
    private Action? _openConfigUi;

    public bool IsServerRunning => _httpServer != null;
    public int ServerPort => _httpServer?.Port ?? 0;
    public int ToolCount => _registry?.ListTools().Count ?? 0;
    public int EventCount => _eventCollector?.Buffer.Count ?? 0;
    public string? LastServerError { get; private set; }

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        Instance = this;
        Service.Initialize(pluginInterface);
        Config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        var eventBuffer = new EventBuffer(2000);
        var eventConfig = Config.EventCollection;
        try { eventConfig.Validate(); }
        catch (ArgumentException ex) { Service.PluginLog.Warning(ex, "事件配置无效，已恢复默认值"); eventConfig = new EventCollectionConfig(); Config.EventCollection = eventConfig; Config.Save(); }
        _eventCollector = new EventCollector(eventBuffer, eventConfig);
        EventTools.Initialize(_eventCollector);

        _pushDataBuffer = new PushDataBuffer(2000);
        _ipcEndpointRegistry = new IpcEndpointRegistry();
        foreach (var endpoint in Config.IpcEndpoints)
        {
            try { _ipcEndpointRegistry.Register(endpoint.PluginName, endpoint.MethodName, endpoint.Signature, endpoint.Description); }
            catch (ArgumentException ex) { Service.PluginLog.Warning(ex, "忽略无效的已保存 IPC 端点"); }
        }
        PluginBridgeTools.Initialize(_pushDataBuffer, _ipcEndpointRegistry);
        _ipcReceiver = new IpcReceiver(_pushDataBuffer);

        InitUi(pluginInterface);
        RegisterCommands();
        StartServer();
    }

    private void InitUi(IDalamudPluginInterface pi)
    {
        _configWindow = new ConfigWindow();
        _windowSystem = new WindowSystem("MCPforDalamud");
        _windowSystem.AddWindow(_configWindow);

        _drawUi = _windowSystem.Draw;
        _openConfigUi = () => _configWindow.IsOpen = true;
        pi.UiBuilder.Draw += _drawUi;
        pi.UiBuilder.OpenConfigUi += _openConfigUi;
    }

    private void RegisterCommands()
    {
        Service.CommandManager.AddHandler("/mcp", new CommandInfo((_, _) =>
        {
            _configWindow!.IsOpen = !_configWindow.IsOpen;
        })
        {
            HelpMessage = "打开/关闭 MCP for Dalamud 设置窗口"
        });
    }

    public void StartServer()
    {
        if (_httpServer != null) return;
        LastServerError = null;
        try
        {
            var registry = new ToolRegistry(new FrameworkRunner());
            PlayerTools.Register(registry);
            TargetTools.Register(registry);
            PartyTools.Register(registry);
            ObjectTableTools.Register(registry);
            MapTools.Register(registry);
            InventoryTools.Register(registry);
            ExcelTools.Register(registry);
            PluginBridgeTools.Register(registry);
            ActionTools.Register(registry);
            EventTools.Register(registry);
            ChatTools.Register(registry);
            MovementTools.Register(registry);

            McpHttpServer? server = null;
            var attempts = Config.Port == 0 ? 3 : 1;
            for (var attempt = 0; attempt < attempts; attempt++)
            {
                server = new McpHttpServer(registry, "127.0.0.1", Config.Port);
                try { server.Start(); break; }
                catch (Exception ex)
                {
                    server.Dispose();
                    server = null;
                    if (attempt + 1 >= attempts) throw new InvalidOperationException("无法启动本地 HTTP 服务", ex);
                }
            }
            if (server == null) throw new InvalidOperationException("无法分配本地 HTTP 端口");
            _registry = registry;
            _httpServer = server;
            Service.ChatGui.Print(string.Format("[MCP] HTTP 服务已启动: http://127.0.0.1:{0}/mcp", server.Port));
        }
        catch (Exception ex)
        {
            _httpServer?.Dispose();
            _httpServer = null;
            _registry = null;
            LastServerError = ex.Message;
            Service.PluginLog.Error(ex, "MCP HTTP 服务启动失败");
            Service.ChatGui.PrintError(string.Format("[MCP] HTTP 服务启动失败: {0}", ex.Message));
        }
    }

    public void StopServer()
    {
        if (_httpServer == null) return;

        _httpServer.Dispose();
        _httpServer = null;
        _registry = null;
        LastServerError = null;

        Service.ChatGui.Print("[MCP] 服务已停止");
    }

    public void Dispose()
    {
        ChatTools.CancelPending();
        StopServer();
        Service.CommandManager.RemoveHandler("/mcp");
        if (_drawUi != null) Service.PluginInterface.UiBuilder.Draw -= _drawUi;
        if (_openConfigUi != null) Service.PluginInterface.UiBuilder.OpenConfigUi -= _openConfigUi;
        _eventCollector?.Dispose();
        _ipcReceiver?.Dispose();
        EventTools.Reset();
        PluginBridgeTools.Reset();
        _windowSystem?.RemoveAllWindows();
        Instance = null;
    }
}
