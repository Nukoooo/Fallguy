using Dalamud.Configuration;

namespace FallGuy;

internal class Configuration : IPluginConfiguration
{
    public bool              Enabled { get; set; } = false;
    public int               Delay   { get; set; } = 0;
    int IPluginConfiguration.Version { get; set; }

    public void Save() => DalamudApi.Interface.SavePluginConfig(this);
}