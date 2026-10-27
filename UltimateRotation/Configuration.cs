using Dalamud.Configuration;

namespace UltimateRotation;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public int LastEncounterId { get; set; } = 1079;

    public string LastJobSpec { get; set; } = string.Empty;

    public bool IncludeOffGcds { get; set; } = true;

    public bool AutoRotation { get; set; }

    public bool UseGemdraught { get; set; }

    public List<uint> DisabledActionIds { get; set; } = [];

    public List<uint> EnabledMitigationIds { get; set; } = [];

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
