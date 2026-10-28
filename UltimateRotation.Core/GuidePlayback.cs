namespace UltimateRotation;

/// <summary>
/// Outside a pull, steps advance when <see cref="TryAdvance"/> matches an action or the user skips.
/// During a pull, <see cref="AlignToPull"/> follows milliseconds since the fight started.
/// Automatic rotation keeps the current part's queue. A skill stays until the game accepts it.
/// The next part starts from <see cref="PartIndexAt"/> on its own log time.
/// </summary>
public sealed class GuidePlayback
{
    private readonly IReadOnlyList<GuideStep> steps;
    private readonly HashSet<int> crossed = [];
    private bool firstSkillUsed;

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
    /// Stays on the current part until the next part's log time, then the first step of that part.
    /// </summary>
    public int PartIndexAt(double elapsedMs)
        => RotationQueue.IndexAt(this.steps, this.Index, elapsedMs);

    public string PhaseName
        => this.Current?.PhaseName
           ?? (this.steps.Count == 0 ? string.Empty : this.steps[^1].PhaseName);

    public bool IsCrossed(int index) => this.crossed.Contains(index);

    /// <summary>The opener has been used, so a later press may mark and jump.</summary>
    public bool FirstSkillUsed => this.firstSkillUsed;

    /// <summary>The pull's first skill has not been used yet. Wrong presses are not errors.</summary>
    public bool WaitingForFirstSkill => !this.firstSkillUsed && !this.IsComplete;

    /// <summary>First step at or after the pull. Earlier steps are pre-pull.</summary>
    public int OpenerIndex
    {
        get
        {
            for (var i = 0; i < this.steps.Count; i++)
            {
                if (this.steps[i].OffsetMs >= 0)
                    return i;
            }

            return 0;
        }
    }

    public bool TryAdvance(uint usedActionId, uint usedAdjustedId)
    {
        var current = this.Current;
        if (current is null || !current.Matches(usedActionId, usedAdjustedId))
            return false;

        this.Index++;
        return true;
    }

    /// <summary>
    /// Before the first skill of the pull is used, a different press does not mark or jump.
    /// Using that skill arms the queue. After that, any later skill in the same part marks
    /// the steps that were jumped and continues after the skill that was pressed.
    /// A skill only in a later part is left alone. This is every step, not one pair.
    /// </summary>
    public bool JumpToUsed(uint usedActionId, uint usedAdjustedId, string? usedName = null)
    {
        var current = this.Current;
        if (current is null)
            return false;

        if (!this.firstSkillUsed && this.MatchesStep(this.steps[this.OpenerIndex], usedActionId, usedAdjustedId, usedName))
        {
            this.firstSkillUsed = true;
            if (this.MatchesStep(current, usedActionId, usedAdjustedId, usedName))
                this.Index++;

            return true;
        }

        if (this.MatchesStep(current, usedActionId, usedAdjustedId, usedName))
        {
            this.Index++;
            return true;
        }

        if (!this.firstSkillUsed)
            return false;

        var phase = current.PhaseId;
        for (var i = this.Index + 1; i < this.steps.Count && this.steps[i].PhaseId == phase; i++)
        {
            if (!this.MatchesStep(this.steps[i], usedActionId, usedAdjustedId, usedName))
                continue;

            for (var skipped = this.Index; skipped < i; skipped++)
                this.crossed.Add(this.steps[skipped].Index);

            this.Index = i + 1;
            return true;
        }

        return false;
    }

    public void Skip()
    {
        if (this.IsComplete)
            return;

        var previous = this.Index;
        this.Index++;
        if (previous == this.OpenerIndex)
            this.firstSkillUsed = true;
    }

    public void Back()
    {
        if (this.Index > 0)
            this.Index--;
    }

    public void Reset()
    {
        this.Index = 0;
        this.crossed.Clear();
        this.firstSkillUsed = false;
    }

    private bool MatchesStep(GuideStep step, uint usedActionId, uint usedAdjustedId, string? usedName)
    {
        if (step.Matches(usedActionId, usedAdjustedId))
            return true;

        return !string.IsNullOrWhiteSpace(usedName)
            && !string.IsNullOrWhiteSpace(step.Name)
            && step.Name.Equals(usedName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Puts the current step on the fight clock. <paramref name="elapsedMs"/> is milliseconds
    /// since pull start. Steps from before the pull stay behind the index.
    /// </summary>
    public void AlignToPull(double elapsedMs)
    {
        this.crossed.Clear();
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
}
