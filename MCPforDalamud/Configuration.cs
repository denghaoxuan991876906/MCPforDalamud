using Dalamud.Configuration;

namespace MCPforDalamud;

public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public int Port { get; set; } = 0;

    public void Save()
    {
        Service.PluginInterface.SavePluginConfig(this);
    }
}
