namespace UltimateRotation;

/// <summary>
/// FFLogs spec slugs for jobs that can enter an ultimate. specName is the unspaced
/// slug from game data (DarkKnight), not the spaced English name (Dark Knight).
/// EnglishName is what the picker shows.
/// </summary>
public static class JobCatalog
{
    public static IReadOnlyList<CombatJob> All { get; } =
    [
        new("Paladin", "Paladin"),
        new("Warrior", "Warrior"),
        new("DarkKnight", "Dark Knight"),
        new("Gunbreaker", "Gunbreaker"),
        new("WhiteMage", "White Mage"),
        new("Scholar", "Scholar"),
        new("Astrologian", "Astrologian"),
        new("Sage", "Sage"),
        new("Monk", "Monk"),
        new("Dragoon", "Dragoon"),
        new("Ninja", "Ninja"),
        new("Samurai", "Samurai"),
        new("Reaper", "Reaper"),
        new("Viper", "Viper"),
        new("Bard", "Bard"),
        new("Machinist", "Machinist"),
        new("Dancer", "Dancer"),
        new("BlackMage", "Black Mage"),
        new("Summoner", "Summoner"),
        new("RedMage", "Red Mage"),
        new("Pictomancer", "Pictomancer"),
    ];

    public static CombatJob? Find(string? specOrName)
    {
        if (string.IsNullOrWhiteSpace(specOrName))
            return null;

        return All.FirstOrDefault(job =>
            job.SpecName.Equals(specOrName, StringComparison.OrdinalIgnoreCase)
            || job.EnglishName.Equals(specOrName, StringComparison.OrdinalIgnoreCase));
    }

    public static bool Matches(CombatJob job, string? reportJob)
    {
        if (string.IsNullOrWhiteSpace(reportJob))
            return false;

        var compact = Compact(reportJob);
        return compact.Equals(Compact(job.SpecName), StringComparison.OrdinalIgnoreCase)
            || compact.Equals(Compact(job.EnglishName), StringComparison.OrdinalIgnoreCase);
    }

    private static string Compact(string value)
    {
        var chars = value.Where(character => !char.IsWhiteSpace(character)).ToArray();
        return new string(chars);
    }
}

public sealed record CombatJob(string SpecName, string EnglishName);
