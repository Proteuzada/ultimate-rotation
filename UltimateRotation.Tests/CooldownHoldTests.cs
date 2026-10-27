using Xunit;

namespace UltimateRotation.Tests;

public class CooldownHoldTests
{
    [Fact]
    public void DoesNotSkipWhileTheCooldownIsAboutToReturn()
    {
        Assert.False(CooldownHold.ShouldSkip(timelineSaysSkip: true, remainingMs: 400));
        Assert.False(CooldownHold.ShouldSkip(timelineSaysSkip: true, remainingMs: 2500));
    }

    [Fact]
    public void SkipsWhenTheCooldownIsReadyOrMuchLater()
    {
        Assert.True(CooldownHold.ShouldSkip(timelineSaysSkip: true, remainingMs: 0));
        Assert.True(CooldownHold.ShouldSkip(timelineSaysSkip: true, remainingMs: 20000));
    }

    [Fact]
    public void KeepsTheStepBeforeTheNextTimestamp()
    {
        Assert.False(CooldownHold.ShouldSkip(timelineSaysSkip: false, remainingMs: 400));
    }
}
