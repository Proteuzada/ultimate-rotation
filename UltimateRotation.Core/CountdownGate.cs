namespace UltimateRotation;

/// <summary>
/// Starts the rotation once, when a countdown reaches 1 second.
/// That is the end of the 1, while the countdown is still showing. Waiting until 0 is late.
/// Hiding the countdown before it reaches 1 does not start it.
/// </summary>
public sealed class CountdownGate
{
    private const float OneSecond = 1f;

    private bool countedDownTowardOne;
    private bool started;

    public bool Observe(bool showing, float remainingSeconds)
    {
        if (!showing)
        {
            if (!this.started)
                this.countedDownTowardOne = false;
            return false;
        }

        if (remainingSeconds > OneSecond)
        {
            this.countedDownTowardOne = true;
            this.started = false;
            return false;
        }

        if (this.started || !this.countedDownTowardOne)
            return false;

        this.started = true;
        this.countedDownTowardOne = false;
        return true;
    }

    public void Reset()
    {
        this.countedDownTowardOne = false;
        this.started = false;
    }
}
