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
    public const string AutoRotationHint = "Off by default. In the selected ultimate, or in combat with a striking dummy, it plays the loaded parse as a queue split by fight part. Each step is pressed in the weave window, not seconds after the GCD is ready. A GCD is sent while its recast is in the last half-second and the animation lock is open or in that same half-second. An oGCD is sent in that lock window as soon as its step time is reached. It is not held for the GCD recast. A late skill does not push the following steps back by those seconds. The next part still starts on its own log time and is not played early. A step is held only while a GCD or a short recast is about to come back. Before the first skill of the pull is used, a step is not skipped and a different press is not marked. After that skill, if the action was already used, or it has no gauge or resource, it is skipped. After that skill, pressing any later skill in the current part marks the skipped steps with an X and continues from that skill. This follows your presses when automatic rotation is off, too. A ground target or an action on another player is still skipped. When the Machinist oGCD on the step is Checkmate or Double Check and that action is not ready, the other is pressed if it is ready. That swap waits for the step's time, and GCDs are not swapped. It does not move you or target other players.";
    public const string QueueButtonPresses = "Queue button presses";
    public const string QueueButtonPressesHint = "On by default. The game stores one press. A GCD is queued in the last half-second of its recast. An oGCD is queued in the last half-second of the animation lock and does not wait for the GCD recast. The next step does not replace a stored press. Off sends an action only when its time is reached and the lock is open.";
    public const string StartOnCountdown = "Start on countdown";
    public const string StartOnCountdownHint = "Off by default. The current step is pressed at the end of the countdown's 1, while that number is still showing. It does not wait until the number disappears. Cancelling before the 1 does nothing.";
    public const string ShowSkills = "Show skills";
    public const string ShowSkillsHint = "Draws each step's skill icon beside the text list. The step being used is highlighted, including while automatic rotation is on. After the first skill of the pull, a step skipped because you pressed any later skill is marked with an X, including in the detached window and when automatic rotation is off.";
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
