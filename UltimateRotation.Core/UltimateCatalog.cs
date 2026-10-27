namespace UltimateRotation;

/// <summary>
/// FFLogs encounter ids and the duty territory type for each ultimate.
/// Territory ids are the instance id from ClientState.TerritoryType.
/// </summary>
public static class UltimateCatalog
{
    public const int Difficulty = 100;
    public const int PartySize = 8;
    public const int RankingLimit = 100;
    public const int RowsPerParse = 6;
    public const int ParseGroupDepth = 3;
    public const int MaxRankingPages = 20;

    public static IReadOnlyList<UltimateEncounter> All { get; } =
    [
        new(1060, "UCoB", "The Unending Coil of Bahamut", 733),
        new(1061, "UwU", "The Weapon's Refrain", 777),
        new(1062, "TEA", "The Epic of Alexander", 887),
        new(1065, "DSR", "Dragonsong's Reprise", 968),
        new(1068, "TOP", "The Omega Protocol", 1122),
        new(1079, "FRU", "Futures Rewritten", 1238),
        new(1085, "UMAD", "Dancing Mad (Ultimate)", 1363),
    ];

    public static UltimateEncounter GetOrFirst(int encounterId)
        => All.FirstOrDefault(encounter => encounter.EncounterId == encounterId) ?? All[0];

    public static UltimateEncounter? FindByTerritory(ushort territoryType)
        => All.FirstOrDefault(encounter => encounter.TerritoryId == territoryType);
}

public sealed record UltimateEncounter(int EncounterId, string Alias, string EnglishName, ushort TerritoryId)
{
    public string Label => $"{Alias} — {EnglishName}";
}
