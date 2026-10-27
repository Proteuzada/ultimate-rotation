namespace UltimateRotation;

internal static class UiText
{
    public const string SetupTitle = "Ultimate Rotation Guide###UltimateRotationSetup";
    public const string OverlayTitle = "Guide###UltimateRotationOverlay";
    public const string Working = "Asking FFLogs…";
    public const string CredentialsHint = "Create a client at fflogs.com/api/clients. The secret stays in your local Dalamud config.";
    public const string GuideOnly = "Guide only. This plugin does not press any ability.";
    public const string CommandHelp = "Opens the rotation guide. /guide overlay shows or hides the overlay.";
    public const string AliasHelp = "Alias of /guide. /guia overlay shows or hides the overlay.";

    public static string Kind(StepKind kind) => kind switch
    {
        StepKind.Gcd => "GCD",
        StepKind.OffGcd => "oGCD",
        _ => "action",
    };
}
