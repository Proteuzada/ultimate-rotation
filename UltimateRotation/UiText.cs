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
    public const string AutoRotationHint = "Off by default. In the selected ultimate, or in combat with a striking dummy, it presses the current step when the clock reaches it, then moves to the next step. If that skill's cooldown has not returned, the step is held for up to 8 seconds and used when it comes back, so the charge is not skipped. A step that can never be used is skipped. It does not move you or target other players.";
    public const string QueueButtonPresses = "Queue button presses";
    public const string QueueButtonPressesHint = "On by default. The plugin holds the next action, the same way a pressed button does. The game can store that press during the last half-second of the animation lock and use it as the lock ends. Off sends the action only when the timeline time is reached.";
    public const string StartOnCountdown = "Start on countdown";
    public const string StartOnCountdownHint = "Off by default. The current step is pressed when the countdown reaches 1, at the end of that second. It does not wait until the countdown finishes. Cancelling before the 1 does nothing.";
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
