namespace UltimateRotation;

public enum StepKind
{
    Gcd,
    OffGcd,
    Other,
}

public readonly record struct AbilityVerdict(bool Drop, StepKind Kind);

public sealed record CastEvent(double TimestampMs, string Type, uint AbilityId, string? Name);

public sealed record PhaseLabel(int Id, string Name, bool IsIntermission);

public sealed record PhaseTransitionMark(int Id, double StartTimeMs);

public sealed record TimelineInput(
    string EncounterName,
    string PlayerName,
    string ReportCode,
    int FightId,
    double FightStartMs,
    double FightEndMs,
    IReadOnlyList<PhaseLabel> Phases,
    IReadOnlyList<PhaseTransitionMark> Transitions,
    IReadOnlyList<CastEvent> Events);

public sealed record GuideStep(
    int Index,
    uint ActionId,
    string Name,
    StepKind Kind,
    int PhaseId,
    string PhaseName)
{
    public uint AdjustedActionId { get; init; }

    public bool Matches(uint usedActionId, uint usedAdjustedId)
    {
        if (usedActionId == 0)
            return false;

        if (usedActionId == ActionId || (AdjustedActionId != 0 && usedActionId == AdjustedActionId))
            return true;

        if (usedAdjustedId == 0 || usedAdjustedId == usedActionId)
            return false;

        return usedAdjustedId == ActionId || usedAdjustedId == AdjustedActionId;
    }
}

public sealed record RankedReport(
    int Rank,
    string Code,
    int FightId,
    double? DurationMs,
    string? Team,
    string? Server);

public sealed record ReportActorInfo(int Id, string Name, string Job, string? Server)
{
    public string Label
    {
        get
        {
            var job = string.IsNullOrWhiteSpace(Job) ? "Job desconhecido" : Job;
            return string.IsNullOrWhiteSpace(Server) ? $"{Name} · {job}" : $"{Name} · {job} · {Server}";
        }
    }
}

public sealed record ReportFightInfo(
    int Id,
    string Name,
    bool Kill,
    double? FightPercentage,
    double StartMs,
    double EndMs,
    bool InProgress,
    IReadOnlyList<int> PlayerIds,
    IReadOnlyList<PhaseTransitionMark> Transitions)
{
    public string Label
    {
        get
        {
            var mark = Kill ? "Abate" : "Wipe";
            var percent = !Kill && FightPercentage is { } value
                ? $" {Math.Clamp(value, 0, 100):0}%"
                : string.Empty;
            return $"Pull {Id} · {FormatDuration(EndMs - StartMs)} · {mark}{percent}";
        }
    }

    public static string FormatDuration(double milliseconds)
    {
        var seconds = Math.Max(0, (int)Math.Round(milliseconds / 1000d));
        return $"{seconds / 60}:{seconds % 60:00}";
    }
}

public sealed record LoadedReport(
    string Code,
    string Title,
    int EncounterId,
    IReadOnlyList<ReportFightInfo> Fights,
    IReadOnlyList<ReportActorInfo> Actors,
    IReadOnlyList<PhaseLabel> Phases,
    IReadOnlyDictionary<uint, string> AbilityNames)
{
    public IReadOnlyList<ReportActorInfo> PlayersIn(ReportFightInfo fight)
    {
        var ids = fight.PlayerIds;
        IEnumerable<ReportActorInfo> players = ids.Count == 0
            ? Actors
            : Actors.Where(actor => ids.Contains(actor.Id));
        return players.OrderBy(actor => actor.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
