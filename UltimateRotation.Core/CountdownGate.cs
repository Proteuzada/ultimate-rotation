namespace UltimateRotation;

/// <summary>
/// Watches a pull countdown. The rotation starts once, when the last second reaches its end.
/// Hiding the countdown while that second is still counting does not start it.
/// </summary>
public sealed class CountdownGate
{
    private const float EndSeconds = 0.05f;
    private const float MissedEndSeconds = 0.2f;

    private bool inLastSecond;
    private float lastRemaining;
    private bool started;

    public bool Observe(bool showing, float remainingSeconds)
    {
        if (showing && remainingSeconds > EndSeconds)
        {
            this.inLastSecond = remainingSeconds <= 1f;
            this.lastRemaining = remainingSeconds;
            this.started = false;
            return false;
        }

        if (this.started || !this.inLastSecond)
        {
            if (!showing)
                this.inLastSecond = false;
            return false;
        }

        var reachedEnd = showing || this.lastRemaining <= MissedEndSeconds;
        if (!reachedEnd)
        {
            this.inLastSecond = false;
            return false;
        }

        this.started = true;
        this.inLastSecond = false;
        return true;
    }

    public void Reset()
    {
        this.inLastSecond = false;
        this.lastRemaining = 0;
        this.started = false;
    }
}
