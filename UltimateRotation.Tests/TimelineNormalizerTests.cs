using Xunit;

namespace UltimateRotation.Tests;

public class TimelineNormalizerTests
{
    [Fact]
    public void KeepsPressOrderAndDropsCastBarCompletion()
    {
        var steps = TimelineNormalizer.Normalize(Input(
            new CastEvent(0, "begincast", 25865, "Broil IV"),
            new CastEvent(1000, "cast", 3571, "Assize"),
            new CastEvent(2500, "cast", 25865, "Broil IV")), Classify);

        Assert.Equal(["Broil IV", "Assize"], steps.Select(step => step.Name).ToArray());
        Assert.Equal([StepKind.Gcd, StepKind.OffGcd], steps.Select(step => step.Kind).ToArray());
    }

    [Fact]
    public void DropsSprintPelotonAutosAndDoesNotInsertDowntime()
    {
        var steps = TimelineNormalizer.Normalize(Input(
            new CastEvent(0, "cast", AbilityNoise.Sprint, "Sprint"),
            new CastEvent(100, "cast", 25865, "Broil IV"),
            new CastEvent(30_000, "cast", AbilityNoise.Peloton, "Peloton"),
            new CastEvent(30_100, "cast", AbilityNoise.AutoAttack, "Attack"),
            new CastEvent(30_200, "cast", 25865, "Broil IV")), Classify);

        Assert.Equal([25865u, 25865u], steps.Select(step => step.ActionId).ToArray());
        Assert.Equal(2, steps.Count);
    }

    [Fact]
    public void StampsPhaseFromReportRelativeTransitions()
    {
        var input = new TimelineInput(
            "Futures Rewritten",
            "Exemplo",
            "abcdefghijklmnop",
            3,
            FightStartMs: 100_000,
            FightEndMs: 400_000,
            Phases:
            [
                new PhaseLabel(1, "Fatebreaker", false),
                new PhaseLabel(2, "Usurper of Frost", false),
            ],
            Transitions:
            [
                new PhaseTransitionMark(1, 100_000),
                new PhaseTransitionMark(2, 180_000),
            ],
            Events:
            [
                new CastEvent(110_000, "cast", 25865, "Broil IV"),
                new CastEvent(190_000, "cast", 3571, "Assize"),
            ]);

        var steps = TimelineNormalizer.Normalize(input, Classify);

        Assert.Equal("Fatebreaker", steps[0].PhaseName);
        Assert.Equal("Usurper of Frost", steps[1].PhaseName);
    }

    [Fact]
    public void KeepsFifteenSecondsOfPrepullAndDropsOlderCasts()
    {
        var input = new TimelineInput(
            "TOP",
            "Exemplo",
            "abcdefghijklmnop",
            1,
            100_000,
            200_000,
            [],
            [],
            [
                new CastEvent(80_000, "cast", 1, "Too early"),
                new CastEvent(90_000, "cast", 2, "Potion"),
                new CastEvent(200_001, "cast", 3, "After"),
            ]);

        var steps = TimelineNormalizer.Normalize(input, id => new AbilityVerdict(false, StepKind.Other));

        Assert.Equal(["Potion"], steps.Select(step => step.Name).ToArray());
    }

    private static TimelineInput Input(params CastEvent[] events)
        => new("FRU", "Exemplo", "abcdefghijklmnop", 1, 0, 0, [], [], events);

    private static AbilityVerdict Classify(uint actionId)
        => actionId == 3571
            ? new AbilityVerdict(false, StepKind.OffGcd)
            : new AbilityVerdict(false, StepKind.Gcd);
}
