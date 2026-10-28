using Xunit;

namespace UltimateRotation.Tests;

public class StuckStepTests
{
    [Fact]
    public void AnEmptyGaugeIsSkippedEvenWhileTheGcdIsAboutToReturn()
    {
        var availability = StuckStep.Classify(
            StuckStep.RequirementNotMet,
            recastRemainMs: 200,
            animationLockMs: 0,
            casting: false);

        Assert.Equal(StepAvailability.Skip, availability);
    }

    [Fact]
    public void AMissingResourceIsSkipped()
    {
        Assert.Equal(StepAvailability.Skip, StuckStep.Classify(StuckStep.NotEnoughMp, 0, 0, false));
        Assert.Equal(StepAvailability.Skip, StuckStep.Classify(StuckStep.NotEnoughEp, 0, 0, false));
    }

    [Fact]
    public void ALongRecastIsHeldUntilThePressCanSucceed()
    {
        var availability = StuckStep.Classify(StuckStep.OnCooldown, recastRemainMs: 12_000, animationLockMs: 0, casting: false);

        Assert.Equal(StepAvailability.Hold, availability);
    }

    [Fact]
    public void ATransientRejectionIsHeldInsteadOfDropped()
    {
        Assert.Equal(StepAvailability.Hold, StuckStep.Classify(StuckStep.CannotExecute, 0, 0, false));
        Assert.Equal(StepAvailability.Hold, StuckStep.Classify(StuckStep.Casting, 0, 0, false));
        Assert.Equal(StepAvailability.Hold, StuckStep.Classify(StuckStep.OnCooldown, 0, 0, false));
    }

    [Fact]
    public void AShortRecastOrGcdInsideTheWeaveWindowIsHeld()
    {
        Assert.Equal(StepAvailability.Hold, StuckStep.Classify(StuckStep.OnCooldown, recastRemainMs: 300, animationLockMs: 0, casting: false));
        Assert.Equal(StepAvailability.Hold, StuckStep.Classify(0, recastRemainMs: 0, animationLockMs: 400, casting: false));
    }

    [Fact]
    public void AnActiveCastIsHeld()
    {
        Assert.Equal(StepAvailability.Hold, StuckStep.Classify(StuckStep.OnCooldown, recastRemainMs: 2_000, animationLockMs: 0, casting: true));
    }

    [Fact]
    public void AReadyActionIsPressed()
    {
        Assert.Equal(StepAvailability.Press, StuckStep.Classify(0, 0, 0, false));
    }

    [Fact]
    public void TheStepIsSkippedOnlyWhenNeitherActionCanBecomeReady()
    {
        Assert.True(StuckStep.ShouldSkip(StepAvailability.Skip, partner: null));
        Assert.True(StuckStep.ShouldSkip(StepAvailability.Skip, StepAvailability.Skip));
        Assert.False(StuckStep.ShouldSkip(StepAvailability.Skip, StepAvailability.Press));
        Assert.False(StuckStep.ShouldSkip(StepAvailability.Skip, StepAvailability.Hold));
        Assert.False(StuckStep.ShouldSkip(StepAvailability.Hold, StepAvailability.Skip));
    }
}
