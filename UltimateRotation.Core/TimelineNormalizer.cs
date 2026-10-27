namespace UltimateRotation;

public static class TimelineNormalizer
{
    public const double PrepullWindowMs = 15_000;
    public const double CastCompletionWindowMs = 12_000;
    public const double DuplicateWindowMs = 50;

    public static IReadOnlyList<GuideStep> Normalize(
        TimelineInput input,
        Func<uint, AbilityVerdict> classify)
    {
        var windowed = FilterWindow(input).ToList();
        var ordered = windowed
            .OrderBy(cast => cast.TimestampMs)
            .ThenBy(cast => IsBeginCast(cast) ? 0 : 1)
            .ToList();

        var openBegins = new Dictionary<uint, Queue<double>>();
        var kept = new List<KeptCast>();

        foreach (var cast in ordered)
        {
            if (!IsBeginCast(cast) && !IsCast(cast))
                continue;

            var dropped = AbilityNoise.IsAlwaysDropped(cast.AbilityId);
            AbilityVerdict verdict = default;
            if (!dropped)
            {
                verdict = classify(cast.AbilityId);
                dropped = verdict.Drop;
            }

            if (IsBeginCast(cast))
            {
                Enqueue(openBegins, cast.AbilityId, cast.TimestampMs);
                if (!dropped)
                    TryKeep(kept, cast, verdict.Kind);
                continue;
            }

            if (TryConsume(openBegins, cast.AbilityId, cast.TimestampMs))
                continue;

            if (!dropped)
                TryKeep(kept, cast, verdict.Kind);
        }

        var phases = input.Phases.ToDictionary(phase => phase.Id);
        var transitions = input.Transitions.OrderBy(transition => transition.StartTimeMs).ToList();
        var opening = OpeningPhase(input, phases, transitions);

        var steps = new List<GuideStep>(kept.Count);
        for (var i = 0; i < kept.Count; i++)
        {
            var cast = kept[i];
            var (phaseId, phaseName) = PhaseAt(cast.TimestampMs, opening, phases, transitions);
            steps.Add(new GuideStep(
                i,
                cast.AbilityId,
                string.IsNullOrWhiteSpace(cast.Name) ? $"Ação {cast.AbilityId}" : cast.Name,
                cast.Kind,
                phaseId,
                phaseName));
        }

        return steps;
    }

    public static AbilityVerdict ApplyOffGcdPreference(AbilityVerdict verdict, bool includeOffGcds)
        => !includeOffGcds && !verdict.Drop && verdict.Kind == StepKind.OffGcd
            ? verdict with { Drop = true }
            : verdict;

    private static IEnumerable<CastEvent> FilterWindow(TimelineInput input)
    {
        if (input.FightEndMs <= input.FightStartMs)
            return input.Events;

        var start = input.FightStartMs - PrepullWindowMs;
        var end = input.FightEndMs;
        return input.Events.Where(cast => cast.TimestampMs >= start && cast.TimestampMs <= end);
    }

    private static void TryKeep(List<KeptCast> kept, CastEvent cast, StepKind kind)
    {
        if (kept.Count > 0)
        {
            var previous = kept[^1];
            if (previous.AbilityId == cast.AbilityId && cast.TimestampMs - previous.TimestampMs < DuplicateWindowMs)
                return;
        }

        kept.Add(new KeptCast(cast.TimestampMs, cast.AbilityId, cast.Name, kind));
    }

    private static void Enqueue(Dictionary<uint, Queue<double>> openBegins, uint abilityId, double timestamp)
    {
        if (!openBegins.TryGetValue(abilityId, out var queue))
        {
            queue = new Queue<double>();
            openBegins[abilityId] = queue;
        }

        queue.Enqueue(timestamp);
    }

    private static bool TryConsume(Dictionary<uint, Queue<double>> openBegins, uint abilityId, double timestamp)
    {
        if (!openBegins.TryGetValue(abilityId, out var queue))
            return false;

        while (queue.Count > 0 && timestamp - queue.Peek() > CastCompletionWindowMs)
            queue.Dequeue();

        if (queue.Count == 0 || queue.Peek() > timestamp)
            return false;

        queue.Dequeue();
        return true;
    }

    private static (int Id, string Name) OpeningPhase(
        TimelineInput input,
        Dictionary<int, PhaseLabel> phases,
        List<PhaseTransitionMark> transitions)
    {
        var atPull = transitions.FirstOrDefault(transition => transition.StartTimeMs <= input.FightStartMs + 500);
        if (atPull is not null)
            return (atPull.Id, DisplayName(phases, atPull.Id));

        var first = phases.Values.OrderBy(phase => phase.Id).FirstOrDefault();
        return first is null ? (1, "Pull") : (first.Id, DisplayName(phases, first.Id));
    }

    private static (int Id, string Name) PhaseAt(
        double timestamp,
        (int Id, string Name) opening,
        Dictionary<int, PhaseLabel> phases,
        List<PhaseTransitionMark> transitions)
    {
        var current = opening;
        foreach (var transition in transitions)
        {
            if (transition.StartTimeMs > timestamp + 0.5)
                break;

            current = (transition.Id, DisplayName(phases, transition.Id));
        }

        return current;
    }

    private static string DisplayName(Dictionary<int, PhaseLabel> phases, int id)
    {
        if (!phases.TryGetValue(id, out var phase) || string.IsNullOrWhiteSpace(phase.Name))
            return phase?.IsIntermission == true ? "Intervalo" : $"Fase {id}";

        return phase.Name;
    }

    private static bool IsBeginCast(CastEvent cast)
        => cast.Type.Equals("begincast", StringComparison.OrdinalIgnoreCase);

    private static bool IsCast(CastEvent cast)
        => cast.Type.Equals("cast", StringComparison.OrdinalIgnoreCase);

    private sealed record KeptCast(double TimestampMs, uint AbilityId, string? Name, StepKind Kind);
}
