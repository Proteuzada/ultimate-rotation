namespace UltimateRotation;

internal static class UiText
{
    public const string SetupTitle = "Ultimate Rotation###UltimateRotationSetup";
    public const string PageParses = "Parses";
    public const string PageActions = "Actions";
    public const string PageRotation = "Rotation";
    public const string ActionsIntro = "Bright icons may be used. Dimmed icons are skipped. Mitigations start dimmed.";
    public const string RotationIntro = "The guide for the loaded parse stays on this page. Automatic rotation stays off until you enable it.";
    public const string Working = "Asking FFLogs…";
    public const string CredentialsHint = "Create a client at fflogs.com/api/clients. The secret stays in your local Dalamud config.";
    public const string GuideOnly = "Automatic rotation is off until you enable it. It only presses your own abilities during the pull.";
    public const string AutoRotation = "Automatic rotation";
    public const string AutoRotationHint = "Off by default. In the selected ultimate, or in combat with a striking dummy, it presses the current step when the clock reaches it, then moves to the next step. A step that cannot be used is skipped. It does not move you or target other players.";
    public const string StartOnCountdown = "Start on countdown";
    public const string StartOnCountdownHint = "Off by default. The current step is pressed as the countdown's last second ends, and the guide starts from that moment. Cancelling the countdown before it finishes does nothing.";
    public const string UseGemdraught = "Use Gemdraught";
    public const string UseGemdraughtHint = "Off by default. On a Gemdraught step, automatic rotation drinks the highest grade in your bags for this job: Strength, Dexterity, Intelligence, or Mind. Off skips that step.";
    public const string CommandHelp = "Opens this window. /guide rotation opens the Rotation page. /guide actions opens the Actions page.";
    public const string AliasHelp = "Alias of /guide. /guia rotation opens the Rotation page. /guia actions opens the Actions page.";

    public static string Kind(StepKind kind) => kind switch
    {
        StepKind.Gcd => "GCD",
        StepKind.OffGcd => "oGCD",
        _ => "action",
    };
}
