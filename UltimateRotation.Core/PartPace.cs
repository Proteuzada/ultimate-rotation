namespace UltimateRotation;

/// <summary>
/// Keeps each part of the log on that part's own timing.
/// A skill used late shifts the rest of the current part by the same amount, so the
/// following skills are not fired together. A part that has not reached its time is
/// not played early. When the fight clock reaches the next part, that part starts
/// from its first step instead of inheriting the previous delay.
/// </summary>
public sealed class PartPace
{
    private const double NoiseMs = 40;
    private int anchoredIndex = -1;

    public double SlipMs { get; private set; }

    public double GuideMs(double elapsedMs) => elapsedMs - this.SlipMs;

    /// <summary>
    /// Anchors a part the first time its opening step is the current step and the
    /// fight clock has already reached that part. An early call leaves the slip alone.
    /// </summary>
    public void ObserveStep(double elapsedMs, int index, bool startsPart, double offsetMs)
    {
        var firstVisit = index != this.anchoredIndex;
        if (firstVisit)
            this.anchoredIndex = index;

        if (!startsPart)
            return;

        if (elapsedMs + NoiseMs < offsetMs)
            return;

        var lateness = Math.Max(0, elapsedMs - offsetMs);
        if (firstVisit || this.SlipMs > lateness + NoiseMs)
            this.SlipMs = lateness;
    }

    /// <summary>Adds the lateness of a skill that was actually used after its place.</summary>
    public void NoteLate(double elapsedMs, double offsetMs)
    {
        var late = this.GuideMs(elapsedMs) - offsetMs;
        if (late > NoiseMs)
            this.SlipMs += late;
    }

    public void Reset()
    {
        this.SlipMs = 0;
        this.anchoredIndex = -1;
    }
}
