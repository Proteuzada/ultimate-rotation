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

    private static GuideStep Step(int index, uint actionId, string name, uint adjusted = 0)
        => new(index, actionId, name, StepKind.Gcd, 1, "Pull")
        {
            AdjustedActionId = adjusted,
        };
}
