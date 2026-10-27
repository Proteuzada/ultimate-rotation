using Xunit;

namespace UltimateRotation.Tests;

public class PartPaceTests
{
    [Fact]
    public void ALateSkillShiftsTheRestOfTheSamePart()
    {
        var pace = new PartPace();
        pace.ObserveStep(0, index: 0, startsPart: true, offsetMs: 0);
        pace.NoteLate(elapsedMs: 800, offsetMs: 0);

        Assert.Equal(800, pace.SlipMs);
        Assert.Equal(1700, pace.GuideMs(2500));
    }

    [Fact]
    public void AnEarlyPartIsNotPulledForward()
    {
        var pace = new PartPace();
        pace.NoteLate(elapsedMs: 3000, offsetMs: 0);
        pace.ObserveStep(elapsedMs: 4000, index: 4, startsPart: true, offsetMs: 10_000);

        Assert.Equal(3000, pace.SlipMs);
        Assert.Equal(1000, pace.GuideMs(4000));
    }

    [Fact]
    public void APartThatWasEarlyStartsWhenItsTimeArrives()
    {
        var pace = new PartPace();
        pace.NoteLate(elapsedMs: 3000, offsetMs: 0);
        pace.ObserveStep(elapsedMs: 4000, index: 4, startsPart: true, offsetMs: 10_000);
        pace.ObserveStep(elapsedMs: 10_000, index: 4, startsPart: true, offsetMs: 10_000);

        Assert.Equal(0, pace.SlipMs);
        Assert.Equal(10_000, pace.GuideMs(10_000));
    }

    [Fact]
    public void TheNextPartStartsOnItsOwnTimeWhenTheFightReachesIt()
    {
        var pace = new PartPace();
        pace.NoteLate(elapsedMs: 4000, offsetMs: 0);
        pace.ObserveStep(elapsedMs: 12_000, index: 4, startsPart: true, offsetMs: 10_000);

        Assert.Equal(2000, pace.SlipMs);
        Assert.Equal(10_000, pace.GuideMs(12_000));
    }

    [Fact]
    public void StaysInsideAPartUntilTheNextPartStarts()
    {
        var playback = new GuidePlayback(
        [
            Step(0, 1, "Broil", phase: 1, offset: 0),
            Step(1, 2, "Assize", phase: 1, offset: 2_500),
            Step(2, 3, "Glare", phase: 2, offset: 60_000),
            Step(3, 4, "Dia", phase: 2, offset: 62_500),
        ]);

        Assert.Equal(0, playback.PartIndexAt(3_000));
        Assert.Equal(2, playback.PartIndexAt(60_000));
        playback.Skip();
        playback.Skip();
        Assert.Equal(2, playback.PartIndexAt(70_000));
    }

    private static GuideStep Step(int index, uint actionId, string name, int phase, double offset)
        => new(index, actionId, name, StepKind.Gcd, phase, $"Phase {phase}", offset);
}
