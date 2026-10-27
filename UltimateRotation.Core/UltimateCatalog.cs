namespace UltimateRotation;

/// <summary>
/// FFLogs encounter ids for ultimates. Dancing Mad (1085) is the last one this list knows.
/// </summary>
public static class UltimateCatalog
{
    public const int Difficulty = 100;
    public const int PartySize = 8;
    public const int RankingLimit = 25;

    public static IReadOnlyList<UltimateEncounter> All { get; } =
    [
        new(1060, "UCoB", "The Unending Coil of Bahamut"),
        new(1061, "UwU", "The Weapon's Refrain"),
        new(1062, "TEA", "The Epic of Alexander"),
        new(1065, "DSR", "Dragonsong's Reprise"),
        new(1068, "TOP", "The Omega Protocol"),
        new(1079, "FRU", "Futures Rewritten"),
        new(1085, "UMAD", "Dancing Mad (Ultimate)"),
    ];

    public static UltimateEncounter GetOrFirst(int encounterId)
        => All.FirstOrDefault(encounter => encounter.EncounterId == encounterId) ?? All[0];
}

public sealed record UltimateEncounter(int EncounterId, string Alias, string EnglishName)
{
    public string Label => $"{Alias} — {EnglishName}";
}
