namespace UltimateRotation;

/// <summary>
/// Presses once at the end of the countdown's 1, while that number is still showing.
/// The displayed digit is the ceiling of the remaining time, so "1" stays up while
/// remaining is still above 0 and at most 1. Remaining of 0 is after the number is gone.
/// Remaining of 1 is when "1" first appears. The press is the last slice of that "1".
/// </summary>
public sealed class CountdownGate
{
    /// <summary>Seconds of the "1" still left when the opener is pressed.</summary>
    public const float EndOfTheOneSeconds = 0.2f;

    private bool sawTheOneComing;
    private bool started;

    public bool Observe(bool running, float remainingSeconds)
    {
        if (!running || remainingSeconds <= 0f)
        {
            if (!this.started)
                this.sawTheOneComing = false;
            return false;
        }

        if (remainingSeconds > EndOfTheOneSeconds)
        {
            if (remainingSeconds > 1f)
                this.sawTheOneComing = true;
            this.started = false;
            return false;
        }

        if (this.started || !this.sawTheOneComing)
            return false;

        this.started = true;
        this.sawTheOneComing = false;
        return true;
    }

    public void Reset()
    {
        this.sawTheOneComing = false;
        this.started = false;
    }
}
