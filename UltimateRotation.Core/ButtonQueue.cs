namespace UltimateRotation;

/// <summary>
/// Decides when a press can succeed. UseAction is rejected while the animation lock is
/// still running, so a GCD is sent only after that lock is clear and its recast is inside
/// the last half-second. An oGCD is sent once the lock is clear and the step time is reached.
/// It does not wait for the GCD recast.
/// </summary>
public static class ButtonQueue
{
    public const double WindowMs = 500;
    private const double LandSlackMs = 40;

    public static bool ShouldPress(
        double elapsedMs,
        double offsetMs,
        double remainingLockMs,
        bool alreadyQueued,
        bool queueEarly)
    {
        if (alreadyQueued)
            return false;

        if (double.IsNaN(remainingLockMs) || remainingLockMs < 0)
            remainingLockMs = 0;

        if (!queueEarly)
            return elapsedMs >= offsetMs;

        if (elapsedMs >= offsetMs)
            return true;

        if (remainingLockMs <= 0 || remainingLockMs > WindowMs)
            return false;

        var firesAt = elapsedMs + remainingLockMs;
        return firesAt + LandSlackMs >= offsetMs;
    }

    /// <summary>
    /// The press belongs in the window where UseAction succeeds. The animation lock must
    /// already be clear: a press during the lock is rejected and the step would be lost.
    /// A GCD is sent only while its recast is inside the last half-second, which is the
    /// game's queue window. An oGCD does not wait for that recast.
    /// </summary>
    public static bool InWeaveWindow(
        double elapsedMs,
        double offsetMs,
        bool gcdStep,
        double animationLockMs,
        double gcdRemainMs,
        bool queueEarly)
    {
        if (double.IsNaN(elapsedMs) || double.IsNaN(offsetMs))
            return false;

        animationLockMs = NonNegative(animationLockMs);
        gcdRemainMs = NonNegative(gcdRemainMs);
        if (animationLockMs > 0)
            return false;

        var ahead = queueEarly ? WindowMs : 0d;
        if (elapsedMs + ahead + LandSlackMs < offsetMs)
            return false;

        if (gcdStep)
            return gcdRemainMs <= ahead;

        return true;
    }

    private static double NonNegative(double value)
        => double.IsNaN(value) || value < 0 ? 0 : value;
}
