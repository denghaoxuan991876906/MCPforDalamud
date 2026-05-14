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

    public bool IsServerRunning => _httpServer != null;
    public int ServerPort => _httpServer?.Port ?? 0;
    public int ToolCount => _registry?.ListTools().Count ?? 0;
    public int EventCount => _eventCollector?.Buffer.Count ?? 0;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        Instance = this;
        Service.Initialize(pluginInterface);
        Config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        var eventBuffer = new EventBuffer(2000);
        var eventConfig = new EventCollectionConfig();
        _eventCollector = new EventCollector(eventBuffer, eventConfig);
        EventTools.Initialize(_eventCollector);

        _pushDataBuffer = new PushDataBuffer(2000);
        _ipcEndpointRegistry = new IpcEndpointRegistry();
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

        pi.UiBuilder.Draw += _windowSystem.Draw;
        pi.UiBuilder.OpenConfigUi += () => _configWindow.IsOpen = true;
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

        var runner = new FrameworkRunner();
        _registry = new ToolRegistry(runner);

        PlayerTools.Register(_registry);
        TargetTools.Register(_registry);
        PartyTools.Register(_registry);
        ObjectTableTools.Register(_registry);
        MapTools.Register(_registry);
        InventoryTools.Register(_registry);
        ExcelTools.Register(_registry);
        PluginBridgeTools.Register(_registry);
        ActionTools.Register(_registry);
        EventTools.Register(_registry);
        ChatTools.Register(_registry);
        MovementTools.Register(_registry);

        _httpServer = new McpHttpServer(_registry, "127.0.0.1", Config.Port);
        _httpServer.Start();

        Config.Port = _httpServer.Port;
        Config.Save();

        Service.ChatGui.Print(string.Format("[MCP] HTTP 服务已启动: http://127.0.0.1:{0}/mcp", _httpServer.Port));
    }

    public void StopServer()
    {
        if (_httpServer == null) return;

        _httpServer.Dispose();
        _httpServer = null;
        _registry = null;

        Service.ChatGui.Print("[MCP] 服务已停止");
    }

    public void Dispose()
    {
        StopServer();
        _eventCollector?.Dispose();
        _ipcReceiver?.Dispose();
        _windowSystem?.RemoveAllWindows();
        Instance = null;
    }
}
