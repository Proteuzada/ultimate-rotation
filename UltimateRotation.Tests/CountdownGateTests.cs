using Xunit;

namespace UltimateRotation.Tests;

public class CountdownGateTests
{
    [Fact]
    public void PressesAtTheEndOfTheOneWhileItIsStillShowing()
    {
        var gate = new CountdownGate();
        Assert.False(gate.Observe(true, 2f));
        Assert.False(gate.Observe(true, 1f));
        Assert.False(gate.Observe(true, 0.5f));
        Assert.True(gate.Observe(true, CountdownGate.EndOfTheOneSeconds));
        Assert.False(gate.Observe(true, 0.05f));
        Assert.False(gate.Observe(false, 0f));
    }

    [Fact]
    public void DoesNotWaitUntilTheNumberDisappears()
    {
        var gate = new CountdownGate();
        Assert.False(gate.Observe(true, 3f));
        Assert.False(gate.Observe(true, 0.21f));
        Assert.True(gate.Observe(true, 0.19f));
    }

    [Fact]
    public void CancelBeforeTheOneDoesNotStart()
    {
        var gate = new CountdownGate();
        Assert.False(gate.Observe(true, 1.4f));
        Assert.False(gate.Observe(false, 0f));
    }

    [Fact]
    public void JoiningDuringTheLastSliceDoesNotStartLate()
    {
        var gate = new CountdownGate();
        Assert.False(gate.Observe(true, 0.15f));
        Assert.False(gate.Observe(true, 0f));
    }

    [Fact]
    public void LaterCountdownCanStartAgain()
    {
        var gate = new CountdownGate();
        Assert.False(gate.Observe(true, 3f));
        Assert.True(gate.Observe(true, 0.2f));

        Assert.False(gate.Observe(true, 5f));
        Assert.False(gate.Observe(true, 1.2f));
        Assert.True(gate.Observe(true, 0.2f));
        Assert.False(gate.Observe(false, 0f));
    }
}
