using Xunit;

namespace UltimateRotation.Tests;

public class GemdraughtTests
{
    [Fact]
    public void RecognizesAGemdraughtStep()
    {
        Assert.True(Gemdraughts.IsStep("Grade 4 Gemdraught of Strength"));
        Assert.False(Gemdraughts.IsStep("Broil IV"));
    }

    [Fact]
    public void StatFollowsTheLogNameOrTheJob()
    {
        Assert.Equal(Gemdraughts.Dexterity, Gemdraughts.StatFor("Gemdraught of Dexterity", "DarkKnight"));
        Assert.Equal(Gemdraughts.Strength, Gemdraughts.StatFor("Gemdraught", "Dark Knight"));
        Assert.Equal(Gemdraughts.Dexterity, Gemdraughts.StatFor("Gemdraught", "Ninja"));
        Assert.Equal(Gemdraughts.Intelligence, Gemdraughts.StatFor("Gemdraught", "Black Mage"));
        Assert.Equal(Gemdraughts.Mind, Gemdraughts.StatFor("Gemdraught", "White Mage"));
    }
}
