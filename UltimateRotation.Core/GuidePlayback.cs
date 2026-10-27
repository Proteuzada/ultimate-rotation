namespace UltimateRotation;

/// <summary>
/// Outside a pull, steps advance when <see cref="TryAdvance"/> matches an action or the user skips.
/// During a pull, <see cref="AlignToPull"/> follows milliseconds since the fight started.
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

    /// <summary>
    /// Puts the current step on the fight clock. <paramref name="elapsedMs"/> is milliseconds
    /// since pull start. Steps from before the pull stay behind the index.
    /// </summary>
    public void AlignToPull(double elapsedMs)
    {
        if (this.steps.Count == 0)
        {
            this.Index = 0;
            return;
        }

        var start = 0;
        while (start < this.steps.Count && this.steps[start].OffsetMs < 0)
            start++;

        if (start >= this.steps.Count)
        {
            this.Index = this.steps.Count;
            return;
        }

        var index = start;
        for (var i = start; i < this.steps.Count; i++)
        {
            if (this.steps[i].OffsetMs <= elapsedMs)
                index = i;
            else
                break;
        }

        this.Index = index;
    }
}
