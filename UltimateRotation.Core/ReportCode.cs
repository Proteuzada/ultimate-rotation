using System.Text.RegularExpressions;

namespace UltimateRotation;

public static partial class ReportCode
{
    public static bool TryExtract(string? text, out string code, out int? fightId)
    {
        code = string.Empty;
        fightId = null;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var trimmed = text.Trim();
        var fight = FightPattern().Match(trimmed);
        if (fight.Success && int.TryParse(fight.Groups[1].Value, out var parsedFight))
            fightId = parsedFight;

        var fromUrl = UrlPattern().Match(trimmed);
        if (fromUrl.Success)
        {
            code = fromUrl.Groups[1].Value;
            return true;
        }

        if (BarePattern().IsMatch(trimmed))
        {
            code = trimmed;
            return true;
        }

        return false;
    }

    [GeneratedRegex(@"fight=(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex FightPattern();

    [GeneratedRegex(@"reports/([A-Za-z0-9]{16})", RegexOptions.IgnoreCase)]
    private static partial Regex UrlPattern();

    [GeneratedRegex(@"^[A-Za-z0-9]{16}$")]
    private static partial Regex BarePattern();
}
