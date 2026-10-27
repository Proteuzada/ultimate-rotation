using Xunit;

namespace UltimateRotation.Tests;

public class ReportCodeTests
{
    [Theory]
    [InlineData("abcdefghijklmnop", "abcdefghijklmnop", null)]
    [InlineData("https://www.fflogs.com/reports/abcdefghijklmnop#fight=12&type=casts", "abcdefghijklmnop", 12)]
    [InlineData("https://fr.fflogs.com/reports/abcdefghijklmnop?fight=4", "abcdefghijklmnop", 4)]
    public void ExtractsCodeAndOptionalFight(string text, string code, int? fightId)
    {
        Assert.True(ReportCode.TryExtract(text, out var parsed, out var parsedFight));
        Assert.Equal(code, parsed);
        Assert.Equal(fightId, parsedFight);
    }

    [Fact]
    public void RejectsAShortCode()
    {
        Assert.False(ReportCode.TryExtract("abc", out _, out _));
    }
}
