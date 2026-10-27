namespace UltimateRotation;

/// <summary>
/// A step whose cooldown has not come back is kept until that cooldown is ready.
/// Skipping it would leave the charge unused. A cooldown that is still many seconds
/// away can be skipped, so one late step does not freeze the rest of the pull.
/// </summary>
public static class CooldownHold
{
    public const double MaxWaitMs = 8000;
    private const double ReadyMs = 20;

    public static bool StillComingBack(double remainingMs)
        => remainingMs > ReadyMs && remainingMs <= MaxWaitMs;

    public static bool ShouldSkip(bool timelineSaysSkip, double remainingMs)
        => timelineSaysSkip && !StillComingBack(remainingMs);
}
