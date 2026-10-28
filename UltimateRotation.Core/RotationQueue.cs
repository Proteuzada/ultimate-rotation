namespace UltimateRotation;

/// <summary>
/// The loaded parse, split into fight parts. A part is one phase of the log.
/// Time that passes inside a part does not skip its skills and does not pull the next part forward.
/// When the fight clock reaches the next part, that part starts at its first step.
/// </summary>
public sealed class RotationQueue
{
    private RotationQueue(IReadOnlyList<GuideStep> steps, IReadOnlyList<RotationPart> parts)
    {
        this.Steps = steps;
        this.Parts = parts;
    }

    public IReadOnlyList<GuideStep> Steps { get; }

    public IReadOnlyList<RotationPart> Parts { get; }

    public static RotationQueue From(IReadOnlyList<GuideStep> steps)
    {
        var parts = new List<RotationPart>();
        var start = 0;
        while (start < steps.Count)
        {
            var phase = steps[start].PhaseId;
            var end = start;
            while (end + 1 < steps.Count && steps[end + 1].PhaseId == phase)
                end++;

            parts.Add(new RotationPart(
                phase,
                steps[start].PhaseName,
                start,
                end,
                steps[start].OffsetMs));
            start = end + 1;
        }

        return new RotationQueue(steps, parts);
    }

    /// <summary>
    /// Stays on the current part until the next part's log time.
    /// Then returns the first step of that part, not a later skill whose timestamp has passed.
    /// </summary>
    public int IndexAt(int index, double elapsedMs)
        => IndexAt(this.Steps, index, elapsedMs);

    public static int IndexAt(IReadOnlyList<GuideStep> steps, int index, double elapsedMs)
    {
        if (index >= steps.Count)
            return index;

        var landed = index;
        for (var i = index + 1; i < steps.Count; i++)
        {
            if (steps[i].PhaseId == steps[i - 1].PhaseId)
                continue;

            if (elapsedMs + 40 < steps[i].OffsetMs)
                break;

            landed = i;
        }

        return landed;
    }

    /// <summary>
    /// The GCD this part is holding, from the current step through the end of the part.
    /// oGCDs in front of it are weaves. They do not replace that GCD.
    /// </summary>
    public static int? HeldGcdIndex(IReadOnlyList<GuideStep> steps, int index)
    {
        if (index < 0 || index >= steps.Count)
            return null;

        var phase = steps[index].PhaseId;
        for (var i = index; i < steps.Count && steps[i].PhaseId == phase; i++)
        {
            if (steps[i].Kind == StepKind.Gcd)
                return i;
        }

        return null;
    }

    public static QueueDecision Decide(
        IReadOnlyList<GuideStep> steps,
        int index,
        double elapsedMs,
        bool slotOccupied,
        bool queueEarly,
        double animationLockMs,
        double gcdRemainMs)
    {
        var held = HeldGcdIndex(steps, index);
        if (slotOccupied || index < 0 || index >= steps.Count)
            return new QueueDecision(null, held);

        var step = steps[index];
        var press = ButtonQueue.InWeaveWindow(
            elapsedMs,
            step.OffsetMs,
            step.Kind == StepKind.Gcd,
            animationLockMs,
            gcdRemainMs,
            queueEarly);
        return new QueueDecision(press ? index : null, held);
    }

    /// <summary>
    /// An oGCD is sent in the animation-lock window, and not in the last half-second of the GCD.
    /// </summary>
    public static bool CanWeave(
        double elapsedMs,
        double offsetMs,
        double animationLockMs,
        double gcdRemainMs,
        bool queueEarly)
        => ButtonQueue.InWeaveWindow(elapsedMs, offsetMs, gcdStep: false, animationLockMs, gcdRemainMs, queueEarly);
}

public sealed record RotationPart(int PhaseId, string Name, int FirstIndex, int LastIndex, double StartMs);

/// <summary>
/// <see cref="PressIndex"/> is the only step that may be sent.
/// <see cref="HeldGcdIndex"/> stays the GCD even while an oGCD is the press.
/// A null press means wait. A full game slot always waits, so a queued press is not replaced.
/// </summary>
public readonly record struct QueueDecision(int? PressIndex, int? HeldGcdIndex);
