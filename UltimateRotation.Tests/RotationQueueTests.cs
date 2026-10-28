using Xunit;

namespace UltimateRotation.Tests;

public class RotationQueueTests
{
    [Fact]
    public void BuildsOneQueuePerFightPart()
    {
        var queue = RotationQueue.From(Sample());

        Assert.Equal(2, queue.Parts.Count);
        Assert.Equal((0, 2, 0d), (queue.Parts[0].FirstIndex, queue.Parts[0].LastIndex, queue.Parts[0].StartMs));
        Assert.Equal((3, 4, 60_000d), (queue.Parts[1].FirstIndex, queue.Parts[1].LastIndex, queue.Parts[1].StartMs));
    }

    [Fact]
    public void TheNextPartStartsAtItsFirstStepInsteadOfBursting()
    {
        var queue = RotationQueue.From(Sample());

        Assert.Equal(0, queue.IndexAt(0, 3_000));
        Assert.Equal(3, queue.IndexAt(0, 60_000));
        Assert.Equal(3, queue.IndexAt(0, 70_000));
    }

    [Fact]
    public void AnEarlyClockDoesNotEnterTheNextPart()
    {
        var queue = RotationQueue.From(Sample());
        Assert.Equal(0, queue.IndexAt(0, 59_000));
    }

    [Fact]
    public void HoldsTheGcdUntilItsRecastIsInsideTheWeaveWindow()
    {
        var steps = Sample();
        var waiting = RotationQueue.Decide(steps, 0, elapsedMs: 1_000, slotOccupied: false, queueEarly: true, animationLockMs: 0, gcdRemainMs: 1_500);
        Assert.Null(waiting.PressIndex);
        Assert.Equal(0, waiting.HeldGcdIndex);

        var ready = RotationQueue.Decide(steps, 0, elapsedMs: 1_000, slotOccupied: false, queueEarly: true, animationLockMs: 0, gcdRemainMs: 400);
        Assert.Equal(0, ready.PressIndex);
        Assert.Equal(0, ready.HeldGcdIndex);

        var locked = RotationQueue.Decide(steps, 0, elapsedMs: 1_000, slotOccupied: false, queueEarly: true, animationLockMs: 200, gcdRemainMs: 400);
        Assert.Null(locked.PressIndex);
        Assert.Equal(0, locked.HeldGcdIndex);
    }

    [Fact]
    public void WeavesTheOffGcdAndKeepsTheLaterGcdHeld()
    {
        var steps = Sample();
        var duringGcd = RotationQueue.Decide(steps, 1, elapsedMs: 3_000, slotOccupied: false, queueEarly: true, animationLockMs: 0, gcdRemainMs: 400);
        Assert.Equal(1, duringGcd.PressIndex);
        Assert.Equal(2, duringGcd.HeldGcdIndex);

        var weave = RotationQueue.Decide(steps, 1, elapsedMs: 3_000, slotOccupied: false, queueEarly: true, animationLockMs: 0, gcdRemainMs: 1_200);
        Assert.Equal(1, weave.PressIndex);
        Assert.Equal(2, weave.HeldGcdIndex);
    }

    [Fact]
    public void DoesNotPressAStepSecondsBeforeItsTime()
    {
        var steps = Sample();
        var early = RotationQueue.Decide(steps, 2, elapsedMs: 1_000, slotOccupied: false, queueEarly: true, animationLockMs: 0, gcdRemainMs: 0);
        Assert.Null(early.PressIndex);
        Assert.Equal(2, early.HeldGcdIndex);
    }

    [Fact]
    public void DoesNotWeaveDuringALongAnimationLockOrReplaceAFullSlot()
    {
        var steps = Sample();

        var waiting = RotationQueue.Decide(steps, 1, elapsedMs: 700, slotOccupied: false, queueEarly: true, animationLockMs: 800, gcdRemainMs: 1_800);
        Assert.Null(waiting.PressIndex);
        Assert.Equal(2, waiting.HeldGcdIndex);

        var occupied = RotationQueue.Decide(steps, 1, elapsedMs: 700, slotOccupied: true, queueEarly: true, animationLockMs: 0, gcdRemainMs: 1_800);
        Assert.Null(occupied.PressIndex);
        Assert.Equal(2, occupied.HeldGcdIndex);
    }

    [Fact]
    public void WeavesAnOffGcdOnceTheLockIsClearWithoutWaitingForTheGcd()
    {
        Assert.False(RotationQueue.CanWeave(600, 900, animationLockMs: 400, gcdRemainMs: 1_500, queueEarly: true));
        Assert.False(RotationQueue.CanWeave(600, 900, animationLockMs: 400, gcdRemainMs: 1_500, queueEarly: false));
        Assert.True(RotationQueue.CanWeave(900, 900, animationLockMs: 0, gcdRemainMs: 1_500, queueEarly: true));
        Assert.False(RotationQueue.CanWeave(1_000, 600, animationLockMs: 800, gcdRemainMs: 1_500, queueEarly: true));
        Assert.True(RotationQueue.CanWeave(3_000, 600, animationLockMs: 0, gcdRemainMs: 400, queueEarly: true));
        Assert.True(RotationQueue.CanWeave(3_000, 600, animationLockMs: 0, gcdRemainMs: 2_000, queueEarly: true));
        Assert.False(RotationQueue.CanWeave(0, 600, animationLockMs: 0, gcdRemainMs: 0, queueEarly: true));
    }

    [Fact]
    public void ALateSkillDoesNotMoveTheNextPart()
    {
        var pace = new PartPace();
        pace.ObserveStep(0, index: 0, startsPart: true, offsetMs: 0);
        pace.NoteLate(elapsedMs: 2_000, offsetMs: 0);
        Assert.Equal(2_000, pace.SlipMs);
        Assert.Equal(500, pace.GuideMs(2_500));

        pace.ObserveStep(60_000, index: 3, startsPart: true, offsetMs: 60_000);
        Assert.Equal(0, pace.SlipMs);
        Assert.Equal(60_000, pace.GuideMs(60_000));
        Assert.True(pace.GuideMs(60_000) < 62_500);
    }

    private static GuideStep[] Sample() =>
    [
        Step(0, "Broil", StepKind.Gcd, phase: 1, offset: 0),
        Step(1, "Assize", StepKind.OffGcd, phase: 1, offset: 600),
        Step(2, "Glare", StepKind.Gcd, phase: 1, offset: 2_500),
        Step(3, "Holy", StepKind.Gcd, phase: 2, offset: 60_000),
        Step(4, "Assize", StepKind.OffGcd, phase: 2, offset: 60_600),
    ];

    private static GuideStep Step(int index, string name, StepKind kind, int phase, double offset)
        => new(index, (uint)(index + 1), name, kind, phase, $"Phase {phase}", offset);
}
