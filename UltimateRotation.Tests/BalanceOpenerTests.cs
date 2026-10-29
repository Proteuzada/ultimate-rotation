using Xunit;

namespace UltimateRotation.Tests;

public class BalanceOpenerTests
{
    [Fact]
    public void EveryJobHasTheBalanceOpener()
    {
        foreach (var job in JobCatalog.All)
        {
            var steps = BalanceOpeners.For(job.SpecName);
            Assert.NotNull(steps);
            Assert.NotEmpty(steps);
            Assert.Contains(steps, step => step.OffsetMs >= 0);
            Assert.All(steps, step => Assert.Equal(BalanceOpeners.PhaseName, step.PhaseName));
            Assert.All(steps, step => Assert.Equal(1, step.PhaseId));
            for (var i = 1; i < steps.Count; i++)
                Assert.True(steps[i].OffsetMs > steps[i - 1].OffsetMs, job.SpecName);
        }
    }

    [Fact]
    public void WhiteMageOpenerKeepsTheBalanceOrder()
    {
        var steps = BalanceOpeners.For("White Mage")!;
        var names = steps.Select(step => step.Name).ToArray();
        Assert.Equal("Glare III", names[0]);
        Assert.Contains("Dia", names);
        Assert.Contains("Presence of Mind", names);
        Assert.Contains("Assize", names);
        Assert.Contains("Afflatus Misery", names);
        Assert.Contains("Gemdraught of Mind", names);
        Assert.True(steps[0].OffsetMs < 0);
        Assert.Contains(steps, step => step.OffsetMs >= 0 && step.Name == "Dia");
    }

    [Fact]
    public void HidingOffGcdsLeavesTheGcdsAndThePotion()
    {
        var steps = BalanceOpeners.For("WhiteMage", includeOffGcds: false)!;
        Assert.DoesNotContain(steps, step => step.Name is "Assize" or "Presence of Mind");
        Assert.Contains(steps, step => step.Name == "Dia");
        Assert.Contains(steps, step => step.Name == "Gemdraught of Mind");
    }

    [Fact]
    public void AfterTheFirstSkillALaterPressMarksTheOpener()
    {
        var steps = BalanceOpeners.For("WhiteMage", includePotion: false)!;
        var playback = new GuidePlayback(steps);
        var glare = steps[0];
        Assert.True(glare.OffsetMs < 0);
        Assert.True(playback.JumpToUsed(glare.ActionId, 0, glare.Name));
        Assert.True(playback.WaitingForFirstSkill);

        var dia = steps.First(step => step.OffsetMs >= 0);
        Assert.Equal("Dia", dia.Name);
        Assert.True(playback.JumpToUsed(dia.ActionId, 0, dia.Name));
        Assert.False(playback.WaitingForFirstSkill);

        var misery = steps.First(step => step.Name == "Afflatus Misery");
        Assert.True(playback.JumpToUsed(misery.ActionId, 0, misery.Name));
        Assert.True(playback.IsCrossed(dia.Index + 1));
        Assert.Equal(misery.Index + 1, playback.Index);
    }

    [Fact]
    public void AnUnknownJobHasNoOpener()
    {
        Assert.Null(BalanceOpeners.For("Blue Mage"));
        Assert.Null(BalanceOpeners.For(null));
    }
}
