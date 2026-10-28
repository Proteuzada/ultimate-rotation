using Xunit;

namespace UltimateRotation.Tests;

public class OffGcdPairsTests
{
    [Fact]
    public void CheckmateAndDoubleCheckAreEachOthersPartner()
    {
        Assert.Equal(OffGcdPairs.DoubleCheck, OffGcdPairs.Partner(OffGcdPairs.Checkmate, 0, "Checkmate"));
        Assert.Equal(OffGcdPairs.Checkmate, OffGcdPairs.Partner(OffGcdPairs.DoubleCheck, 0, "Double Check"));
        Assert.Equal(OffGcdPairs.DoubleCheck, OffGcdPairs.Partner(38130, 0, null));
        Assert.Equal(OffGcdPairs.Checkmate, OffGcdPairs.Partner(0, 38851, "Double Check"));
    }

    [Fact]
    public void AnUnpairedActionHasNoPartner()
    {
        Assert.Null(OffGcdPairs.Partner(161, 0, "Broil"));
    }

    [Fact]
    public void DoesNotSwapAGcdOrBeforeTheStepTime()
    {
        Assert.False(OffGcdPairs.MaySwap(StepKind.Gcd, guideMs: 1_000, offsetMs: 1_000));
        Assert.False(OffGcdPairs.MaySwap(StepKind.OffGcd, guideMs: 999, offsetMs: 1_000));
        Assert.True(OffGcdPairs.MaySwap(StepKind.OffGcd, guideMs: 1_000, offsetMs: 1_000));
    }
}
