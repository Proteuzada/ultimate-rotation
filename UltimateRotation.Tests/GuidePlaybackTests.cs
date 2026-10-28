using Xunit;

namespace UltimateRotation.Tests;

public class GuidePlaybackTests
{
    [Fact]
    public void AdvancesOnMatchingActionOrSkipAndIgnoresEverythingElse()
    {
        var playback = new GuidePlayback(
        [
            Step(0, 100, "A", adjusted: 200),
            Step(1, 101, "B"),
        ]);

        Assert.False(playback.TryAdvance(999, 999));
        Assert.Equal(0, playback.Index);
        Assert.Equal("A", playback.Current!.Name);

        Assert.True(playback.TryAdvance(50, 100));
        Assert.Equal("B", playback.Current!.Name);

        playback.Skip();
        Assert.True(playback.IsComplete);
        Assert.False(playback.TryAdvance(101, 101));

        playback.Back();
        Assert.Equal("B", playback.Current!.Name);
        playback.Reset();
        Assert.Equal("A", playback.Current!.Name);
    }

    [Fact]
    public void StartsAtPullAndFollowsElapsedFightTime()
    {
        var playback = new GuidePlayback(
        [
            Step(0, 1, "Potion", offset: -10_000),
            Step(1, 2, "Broil", offset: 0),
            Step(2, 3, "Assize", offset: 2_500),
        ]);

        playback.AlignToPull(0);
        Assert.Equal("Broil", playback.Current!.Name);

        playback.AlignToPull(2_499);
        Assert.Equal("Broil", playback.Current!.Name);

        playback.AlignToPull(2_500);
        Assert.Equal("Assize", playback.Current!.Name);
    }

    [Fact]
    public void StaysOnASkillWhenTheNextTimestampArrives()
    {
        var playback = new GuidePlayback(
        [
            Step(0, 1, "Broil", offset: 0),
            Step(1, 2, "Assize", offset: 2_500),
        ]);

        Assert.False(playback.IsDue(-1));
        Assert.True(playback.IsDue(2_500));
        Assert.Equal(0, playback.Index);
        Assert.Equal(0, playback.PartIndexAt(2_500));
    }

    [Fact]
    public void UsingALaterSkillMarksTheSkippedStepsAndContinuesFromIt()
    {
        var playback = new GuidePlayback(
        [
            Step(0, 10, "Reassemble"),
            Step(1, 20, "Drill"),
            Step(2, 30, "Air Anchor"),
            Step(3, 40, "Drill", phase: 2),
        ]);

        Assert.True(playback.JumpToUsed(20, 20));
        Assert.True(playback.IsCrossed(0));
        Assert.False(playback.IsCrossed(1));
        Assert.Equal("Air Anchor", playback.Current!.Name);

        Assert.False(playback.JumpToUsed(40, 40));
        Assert.Equal("Air Anchor", playback.Current!.Name);
    }

    [Fact]
    public void UsingTheCurrentSkillAdvancesWithoutACross()
    {
        var playback = new GuidePlayback(
        [
            Step(0, 10, "Reassemble"),
            Step(1, 20, "Drill"),
        ]);

        Assert.True(playback.JumpToUsed(10, 10));
        Assert.False(playback.IsCrossed(0));
        Assert.Equal("Drill", playback.Current!.Name);

        playback.Reset();
        Assert.Equal("Reassemble", playback.Current!.Name);
        Assert.False(playback.IsCrossed(0));
    }

    private static GuideStep Step(int index, uint actionId, string name, uint adjusted = 0, double offset = 0, int phase = 1)
        => new(index, actionId, name, StepKind.Gcd, phase, phase == 1 ? "Pull" : "Phase 2", offset)
        {
            AdjustedActionId = adjusted,
        };
}
