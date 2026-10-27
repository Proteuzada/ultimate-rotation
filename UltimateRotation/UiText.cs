namespace UltimateRotation;

internal static class UiText
{
    public const string SetupTitle = "Ultimate Rotation Guide###UltimateRotationSetup";
    public const string OverlayTitle = "Guide###UltimateRotationOverlay";
    public const string Working = "Asking FFLogs…";
    public const string CredentialsHint = "Create a client at fflogs.com/api/clients. The secret stays in your local Dalamud config.";
    public const string GuideOnly = "Automatic rotation is off until you enable it. It only presses your own abilities during the pull.";
    public const string AutoRotation = "Automatic rotation";
    public const string AutoRotationHint = "Off by default. In the selected ultimate, or in combat with a striking dummy, it presses the current step when the clock reaches it, then moves to the next step. A step that cannot be used is skipped. It does not move you or target other players.";
    public const string UseGemdraught = "Use Gemdraught";
    public const string UseGemdraughtHint = "Off by default. On a Gemdraught step, automatic rotation drinks the highest grade in your bags for this job: Strength, Dexterity, Intelligence, or Mind. Off skips that step.";
    public const string CommandHelp = "Opens the rotation guide. /guide overlay shows or hides the overlay.";
    public const string AliasHelp = "Alias of /guide. /guia overlay shows or hides the overlay.";

    public static string Kind(StepKind kind) => kind switch
    {
        StepKind.Gcd => "GCD",
        StepKind.OffGcd => "oGCD",
        _ => "action",
    };
}
