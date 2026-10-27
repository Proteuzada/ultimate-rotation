using Xunit;

namespace UltimateRotation.Tests;

public class CountdownGateTests
{
    [Fact]
    public void StartsAtTheEndOfTheOneAndNotAfterTheCountdown()
    {
        var gate = new CountdownGate();
        Assert.False(gate.Observe(true, 2f));
        Assert.False(gate.Observe(true, 1.05f));
        Assert.True(gate.Observe(true, 1f));
        Assert.False(gate.Observe(true, 0.2f));
        Assert.False(gate.Observe(true, 0f));
    }

    [Fact]
    public void CancelBeforeTheOneDoesNotStart()
    {
        var gate = new CountdownGate();
        Assert.False(gate.Observe(true, 1.4f));
        Assert.False(gate.Observe(false, 0f));
    }

    [Fact]
    public void JoiningDuringTheLastSecondDoesNotStartLate()
    {
        var gate = new CountdownGate();
        Assert.False(gate.Observe(true, 0.4f));
        Assert.False(gate.Observe(true, 0f));
    }

    [Fact]
    public void LaterCountdownCanStartAgain()
    {
        var gate = new CountdownGate();
        Assert.False(gate.Observe(true, 3f));
        Assert.True(gate.Observe(true, 0.98f));

        Assert.False(gate.Observe(true, 5f));
        Assert.False(gate.Observe(true, 1.2f));
        Assert.True(gate.Observe(true, 0.9f));
        Assert.False(gate.Observe(false, 0f));
    }
}
