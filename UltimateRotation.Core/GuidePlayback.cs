namespace UltimateRotation;

/// <summary>
/// Steps advance only when <see cref="TryAdvance"/> matches an action or when the user skips.
/// There is no timer.
/// </summary>
public sealed class GuidePlayback
{
    private readonly IReadOnlyList<GuideStep> steps;

    public GuidePlayback(IReadOnlyList<GuideStep> steps)
    {
        this.steps = steps;
    }

    public IReadOnlyList<GuideStep> Steps => this.steps;

    public int Index { get; private set; }

    public bool IsComplete => this.Index >= this.steps.Count;

    public GuideStep? Current => this.IsComplete ? null : this.steps[this.Index];

    public string PhaseName
        => this.Current?.PhaseName
           ?? (this.steps.Count == 0 ? string.Empty : this.steps[^1].PhaseName);

    public bool TryAdvance(uint usedActionId, uint usedAdjustedId)
    {
        var current = this.Current;
        if (current is null || !current.Matches(usedActionId, usedAdjustedId))
            return false;

        this.Index++;
        return true;
    }

    public void Skip()
    {
        if (!this.IsComplete)
            this.Index++;
    }

    public void Back()
    {
        if (this.Index > 0)
            this.Index--;
    }

    public void Reset() => this.Index = 0;
}
