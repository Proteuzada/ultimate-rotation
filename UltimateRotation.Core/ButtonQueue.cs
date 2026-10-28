namespace UltimateRotation;

/// <summary>
/// Decides when to hold a button. The game stores one press during the last half-second
/// of the animation lock and fires it as that lock ends. An immediate press waits for the timeline.
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
    /// The press belongs in the weave window. A GCD is sent only while its recast is inside
    /// that half-second. An oGCD has no GCD wait: it is sent in the animation-lock window
    /// as soon as the step time is reached, even while the GCD recast is still running.
    /// Nothing is sent before the step's time except that same window.
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

        var ahead = queueEarly ? WindowMs : 0d;
        if (elapsedMs + ahead + LandSlackMs < offsetMs)
            return false;

        var lockOpen = animationLockMs <= 0 || (queueEarly && animationLockMs <= WindowMs);
        if (!lockOpen)
            return false;

        if (gcdStep)
            return gcdRemainMs <= ahead;

        return true;
    }

    private static double NonNegative(double value)
        => double.IsNaN(value) || value < 0 ? 0 : value;
}
