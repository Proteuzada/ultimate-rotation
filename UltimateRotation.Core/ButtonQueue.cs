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
}
