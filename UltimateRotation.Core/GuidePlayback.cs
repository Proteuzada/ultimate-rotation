namespace UltimateRotation;

/// <summary>
/// Outside a pull, steps advance when <see cref="TryAdvance"/> matches an action or the user skips.
/// During a pull, <see cref="AlignToPull"/> follows milliseconds since the fight started.
/// Automatic rotation uses <see cref="IsDue"/> and <see cref="ShouldSkipUnusable"/> so a step is
/// pressed at its clock time and skipped once the next step is due.
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

    /// <summary>The current step is the first step of its phase, including the opener.</summary>
    public bool CurrentStartsPart
    {
        get
        {
            if (this.Current is null)
                return false;

            if (this.Index <= 0)
                return true;

            return this.steps[this.Index].PhaseId != this.steps[this.Index - 1].PhaseId;
        }
    }

    /// <summary>
    /// Stays on the current part until the next part's time. When that time has
    /// arrived, returns the first step of the latest part the fight clock has reached.
    /// Skills inside a part are not skipped just because their timestamps have passed.
    /// </summary>
    public int PartIndexAt(double elapsedMs)
    {
        var index = this.Index;
        if (index >= this.steps.Count)
            return index;

        for (var i = index + 1; i < this.steps.Count; i++)
        {
            if (this.steps[i].PhaseId == this.steps[i - 1].PhaseId)
                continue;

            if (elapsedMs + 40 < this.steps[i].OffsetMs)
                break;

            index = i;
        }

        return index;
    }

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

    public bool IsDue(double elapsedMs)
    {
        var current = this.Current;
        return current is not null && elapsedMs >= current.OffsetMs;
    }

    /// <summary>
    /// A step that cannot be used is kept until the following step is due, then skipped.
    /// The last step is skipped as soon as it is due and unusable.
    /// </summary>
    public bool ShouldSkipUnusable(double elapsedMs)
    {
        if (!this.IsDue(elapsedMs))
            return false;

        var next = this.Index + 1;
        if (next >= this.steps.Count)
            return true;

        return elapsedMs >= this.steps[next].OffsetMs;
    }
}
