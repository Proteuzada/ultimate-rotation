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
    public const string AutoRotationHint = "Off by default. In the selected ultimate, or in combat with a striking dummy, it plays the loaded parse as a queue split by fight part. A delay or an early press stays in that part. The next part starts on its own log time: it does not keep the previous delay, and it does not fire its skills in a burst. A skill the game can cast is held until the game accepts it. Only a ground target or an action on another player is skipped. When the Machinist oGCD on the step is Checkmate or Double Check and that action is not ready, the other is pressed if it is ready. That swap waits for the step's time, and GCDs are not swapped. It does not move you or target other players.";
    public const string QueueButtonPresses = "Queue button presses";
    public const string QueueButtonPressesHint = "On by default. The GCD stays the held button. An oGCD is weaved while the animation lock is in its last half-second, or as soon as that lock is open. The game stores one press, and the next step does not replace it. Off sends an action only when its time is reached and the lock is open.";
    public const string StartOnCountdown = "Start on countdown";
    public const string StartOnCountdownHint = "Off by default. The current step is pressed at the end of the countdown's 1, while that number is still showing. It does not wait until the number disappears. Cancelling before the 1 does nothing.";
    public const string ShowSkills = "Show skills";
    public const string ShowSkillsHint = "Draws each step's skill icon beside the text list. The step being used is highlighted, including while automatic rotation is on.";
    public const string SkillsWindow = "Skills###UltimateRotationSkills";
    public const string Detach = "Detach";
    public const string Attach = "Attach";
    public const string DetachHint = "Opens this icon timeline in its own window. The step being used stays highlighted, including while automatic rotation is on.";
    public const string AttachHint = "Puts the icon timeline back beside the text list.";
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
