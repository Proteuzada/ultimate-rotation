namespace UltimateRotation;

/// <summary>
/// A step whose cooldown has not come back is kept until that cooldown is ready.
/// Skipping it would leave the charge unused. The wait is one GCD. A longer
/// cooldown is skipped so the rest of the part is not frozen.
/// </summary>
public static class CooldownHold
{
    public const double MaxWaitMs = 2500;
    private const double ReadyMs = 20;

    public static bool StillComingBack(double remainingMs)
        => remainingMs > ReadyMs && remainingMs <= MaxWaitMs;

    public static bool ShouldSkip(bool timelineSaysSkip, double remainingMs)
        => timelineSaysSkip && !StillComingBack(remainingMs);
}
