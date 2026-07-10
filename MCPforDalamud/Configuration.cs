using Dalamud.Configuration;
using MCPforDalamud.Ipc;
using MCPforDalamud.Events;

namespace MCPforDalamud;

public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public int Port { get; set; } = 0;
    public List<IpcEndpoint> IpcEndpoints { get; set; } = new();
    public EventCollectionConfig EventCollection { get; set; } = new();
    public bool AllowActions { get; set; }
    public bool AllowMovement { get; set; }
    public bool AllowChat { get; set; }
    public bool AllowPluginManagement { get; set; }

    public void Save()
    {
        Service.PluginInterface.SavePluginConfig(this);
    }
}
