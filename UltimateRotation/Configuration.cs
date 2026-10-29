using Dalamud.Configuration;

namespace UltimateRotation;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 2;

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public int LastEncounterId { get; set; } = 1079;

    public string LastJobSpec { get; set; } = string.Empty;

    public bool IncludeOffGcds { get; set; } = true;

    public bool AutoRotation { get; set; }

    public bool StartOnCountdown { get; set; }

    public bool QueueButtonPresses { get; set; } = true;

    public bool UseGemdraught { get; set; }

    public bool ShowSkills { get; set; }

    public bool SkillsDetached { get; set; }

    public bool OpenerTraining { get; set; }

    public List<uint> DisabledActionIds { get; set; } = [];

    public List<uint> EnabledMitigationIds { get; set; } = [];

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
