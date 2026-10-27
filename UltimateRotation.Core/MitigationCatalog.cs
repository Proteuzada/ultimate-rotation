namespace UltimateRotation;

/// <summary>
/// Defensive cooldowns. They are optional: automatic rotation skips them until the Actions menu enables them.
/// </summary>
public static class MitigationCatalog
{
    public static bool IsMitigation(uint actionId, string? name)
        => ById.ContainsKey(actionId)
           || (!string.IsNullOrWhiteSpace(name) && Names.Contains(name));

    public static IReadOnlyList<MitigationAction> ForJob(string? specName)
    {
        var spec = JobCatalog.Find(specName)?.SpecName;
        var list = new List<MitigationAction>();
        if (spec is "Paladin" or "Warrior" or "DarkKnight" or "Gunbreaker")
            list.AddRange(TankRole);
        else if (spec is "Monk" or "Dragoon" or "Ninja" or "Samurai" or "Reaper" or "Viper")
            list.AddRange(MeleeRole);
        else if (spec is "BlackMage" or "Summoner" or "RedMage" or "Pictomancer" or "WhiteMage" or "Scholar" or "Astrologian" or "Sage")
            list.AddRange(CasterRole);

        if (spec is not null && JobActions.TryGetValue(spec, out var own))
            list.AddRange(own);

        return list;
    }

    private static readonly MitigationAction[] TankRole =
    [
        new(7531, "Rampart"),
        new(7535, "Reprisal"),
        new(7548, "Arm's Length"),
    ];

    private static readonly MitigationAction[] MeleeRole =
    [
        new(7549, "Feint"),
        new(7548, "Arm's Length"),
    ];

    private static readonly MitigationAction[] CasterRole =
    [
        new(7560, "Addle"),
        new(7559, "Surecast"),
    ];

    private static readonly Dictionary<string, MitigationAction[]> JobActions = new()
    {
        ["Paladin"] =
        [
            new(3542, "Sheltron"),
            new(25746, "Holy Sheltron"),
            new(17, "Sentinel"),
            new(30, "Hallowed Ground"),
            new(3540, "Divine Veil"),
            new(7385, "Passage of Arms"),
            new(7382, "Intervention"),
            new(27, "Cover"),
        ],
        ["Warrior"] =
        [
            new(44, "Vengeance"),
            new(36923, "Damnation"),
            new(3551, "Raw Intuition"),
            new(25751, "Bloodwhetting"),
            new(40, "Thrill of Battle"),
            new(7388, "Shake It Off"),
            new(16464, "Nascent Flash"),
            new(43, "Holmgang"),
        ],
        ["DarkKnight"] =
        [
            new(7393, "The Blackest Night"),
            new(25754, "Oblation"),
            new(3636, "Shadow Wall"),
            new(36927, "Shadowed Vigil"),
            new(3634, "Dark Mind"),
            new(16471, "Dark Missionary"),
            new(3638, "Living Dead"),
        ],
        ["Gunbreaker"] =
        [
            new(16161, "Heart of Stone"),
            new(25758, "Heart of Corundum"),
            new(16148, "Nebula"),
            new(36935, "Great Nebula"),
            new(16140, "Camouflage"),
            new(16160, "Heart of Light"),
            new(16152, "Superbolide"),
        ],
        ["Monk"] = [new(65, "Mantra")],
        ["Bard"] = [new(7405, "Troubadour")],
        ["Machinist"] = [new(16889, "Tactician")],
        ["Dancer"] = [new(16012, "Shield Samba")],
        ["WhiteMage"] =
        [
            new(7432, "Divine Benison"),
            new(25861, "Aquaveil"),
            new(3569, "Asylum"),
            new(16536, "Temperance"),
            new(25862, "Liturgy of the Bell"),
        ],
        ["Scholar"] =
        [
            new(188, "Sacred Soil"),
            new(25867, "Protraction"),
            new(25868, "Expedient"),
            new(16538, "Fey Illumination"),
            new(3585, "Deployment Tactics"),
            new(37014, "Seraphism"),
        ],
        ["Astrologian"] =
        [
            new(3613, "Collective Unconscious"),
            new(16553, "Celestial Opposition"),
            new(16559, "Neutral Sect"),
            new(25873, "Exaltation"),
            new(25874, "Macrocosmos"),
        ],
        ["Sage"] =
        [
            new(24298, "Kerachole"),
            new(24303, "Taurochole"),
            new(24305, "Haima"),
            new(24311, "Panhaima"),
            new(24310, "Holos"),
            new(24317, "Krasis"),
            new(24294, "Soteria"),
        ],
    };

    private static readonly Dictionary<uint, string> ById = BuildIndex();

    private static readonly HashSet<string> Names = new(ById.Values, StringComparer.OrdinalIgnoreCase);

    private static Dictionary<uint, string> BuildIndex()
    {
        var index = new Dictionary<uint, string>();
        foreach (var action in TankRole.Concat(MeleeRole).Concat(CasterRole).Concat(JobActions.Values.SelectMany(actions => actions)))
            index[action.ActionId] = action.Name;
        return index;
    }
}

public sealed record MitigationAction(uint ActionId, string Name);
