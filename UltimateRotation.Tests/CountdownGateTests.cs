using Xunit;

namespace UltimateRotation.Tests;

public class CountdownGateTests
{
    [Fact]
    public void StartsWhenTheLastSecondEnds()
    {
        var gate = new CountdownGate();
        Assert.False(gate.Observe(true, 2f));
        Assert.False(gate.Observe(true, 0.8f));
        Assert.True(gate.Observe(true, 0.04f));
        Assert.False(gate.Observe(true, 0f));
    }

    [Fact]
    public void CancelDuringTheLastSecondDoesNotStart()
    {
        var gate = new CountdownGate();
        Assert.False(gate.Observe(true, 0.7f));
        Assert.False(gate.Observe(false, 0f));
    }

    [Fact]
    public void HiddenJustAfterZeroStillStartsOnce()
    {
        var gate = new CountdownGate();
        Assert.False(gate.Observe(true, 0.12f));
        Assert.True(gate.Observe(false, 0f));
        Assert.False(gate.Observe(false, 0f));
    }

    [Fact]
    public void LaterCountdownCanStartAgain()
    {
        var gate = new CountdownGate();
        Assert.False(gate.Observe(true, 0.8f));
        Assert.True(gate.Observe(true, 0.02f));

        Assert.False(gate.Observe(true, 5f));
        Assert.False(gate.Observe(true, 0.4f));
        Assert.True(gate.Observe(true, 0.01f));
    }
}
