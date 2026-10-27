using Xunit;

namespace UltimateRotation.Tests;

public class ButtonQueueTests
{
    [Fact]
    public void ImmediatePressWaitsForTheTimeline()
    {
        Assert.False(ButtonQueue.ShouldPress(1000, 1500, 0, false, true));
        Assert.True(ButtonQueue.ShouldPress(1500, 1500, 0, false, true));
    }

    [Fact]
    public void QueuesWhenTheLockWouldLandOnTheStep()
    {
        Assert.True(ButtonQueue.ShouldPress(1100, 1500, 400, false, true));
        Assert.False(ButtonQueue.ShouldPress(800, 1500, 400, false, true));
    }

    [Fact]
    public void DoesNotPressWhileAButtonIsAlreadyQueued()
    {
        Assert.False(ButtonQueue.ShouldPress(1600, 1500, 0, true, true));
    }

    [Fact]
    public void HoldsTheButtonAfterTheStepIsDue()
    {
        Assert.True(ButtonQueue.ShouldPress(1600, 1500, 800, false, true));
    }

    [Fact]
    public void WithoutQueueOnlyTheTimelineTimePresses()
    {
        Assert.False(ButtonQueue.ShouldPress(1400, 1500, 100, false, false));
        Assert.True(ButtonQueue.ShouldPress(1500, 1500, 100, false, false));
    }
}
