using Xunit;

namespace UltimateRotation.Tests;

public class ActionMenuTests
{
    [Fact]
    public void MitigationsStartOffAndOtherActionsStartOn()
    {
        Assert.False(ActionMenu.Allows(7531, "Rampart", false, Empty, Empty));
        Assert.True(ActionMenu.Allows(7531, "Rampart", false, Empty, new HashSet<uint> { 7531 }));
        Assert.True(ActionMenu.Allows(3617, "Hard Slash", false, Empty, Empty));
        Assert.False(ActionMenu.Allows(3617, "Hard Slash", false, new HashSet<uint> { 3617 }, Empty));
        Assert.False(ActionMenu.Allows(0, "Grade 4 Gemdraught of Strength", false, Empty, Empty));
        Assert.True(ActionMenu.Allows(0, "Grade 4 Gemdraught of Strength", true, Empty, Empty));
    }

    [Fact]
    public void MenuListsJobMitigationsAndParseActions()
    {
        var log = new List<LogAction>
        {
            new(3617, "Hard Slash", StepKind.Gcd),
            new(7531, "Rampart", StepKind.OffGcd),
        };

        var menu = ActionMenu.Build("DarkKnight", log, true, false, Empty, Empty);

        Assert.Contains(menu, choice => choice.Name == "Rampart" && choice.Mitigation && !choice.Enabled);
        Assert.Contains(menu, choice => choice.Name == "The Blackest Night" && !choice.Enabled);
        Assert.Contains(menu, choice => choice.Name == "Hard Slash" && !choice.Mitigation && choice.Enabled && choice.InParse);
        Assert.Contains(menu, choice => choice.Name == "The Blackest Night" && !choice.InParse);
        Assert.Contains(menu, choice => choice.Potion && !choice.Enabled);
        Assert.DoesNotContain(menu, choice => choice.Name == "Feint");
    }

    private static HashSet<uint> Empty => [];
}
