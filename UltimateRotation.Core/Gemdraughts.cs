namespace UltimateRotation;

/// <summary>
/// The raid potion is a Gemdraught. The stat follows the job, unless the log step names one.
/// </summary>
public static class Gemdraughts
{
    public const string Strength = "Strength";
    public const string Dexterity = "Dexterity";
    public const string Intelligence = "Intelligence";
    public const string Mind = "Mind";

    public static bool IsStep(string? name)
        => !string.IsNullOrWhiteSpace(name)
           && name.Contains("Gemdraught", StringComparison.OrdinalIgnoreCase);

    public static string StatFor(string? stepName, string? specName)
    {
        if (Contains(stepName, Strength))
            return Strength;
        if (Contains(stepName, Dexterity))
            return Dexterity;
        if (Contains(stepName, Intelligence))
            return Intelligence;
        if (Contains(stepName, Mind))
            return Mind;

        var job = JobCatalog.Find(specName);
        return job?.SpecName switch
        {
            "Ninja" or "Viper" or "Bard" or "Machinist" or "Dancer" => Dexterity,
            "BlackMage" or "Summoner" or "RedMage" or "Pictomancer" => Intelligence,
            "WhiteMage" or "Scholar" or "Astrologian" or "Sage" => Mind,
            _ => Strength,
        };
    }

    private static bool Contains(string? text, string word)
        => !string.IsNullOrWhiteSpace(text)
           && text.Contains(word, StringComparison.OrdinalIgnoreCase);
}
