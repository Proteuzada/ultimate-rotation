using Xunit;

namespace UltimateRotation.Tests;

public class UltimateCatalogTests
{
    [Fact]
    public void TerritoryIdsAreTheDutyInstanceIds()
    {
        Assert.Equal((ushort)733, UltimateCatalog.GetOrFirst(1060).TerritoryId);
        Assert.Equal((ushort)777, UltimateCatalog.GetOrFirst(1061).TerritoryId);
        Assert.Equal((ushort)887, UltimateCatalog.GetOrFirst(1062).TerritoryId);
        Assert.Equal((ushort)968, UltimateCatalog.GetOrFirst(1065).TerritoryId);
        Assert.Equal((ushort)1122, UltimateCatalog.GetOrFirst(1068).TerritoryId);
        Assert.Equal((ushort)1238, UltimateCatalog.GetOrFirst(1079).TerritoryId);
        Assert.Equal((ushort)1363, UltimateCatalog.GetOrFirst(1085).TerritoryId);
        Assert.Equal(1085, UltimateCatalog.FindByTerritory(1363)!.EncounterId);
    }

    [Fact]
    public void SpecNamesAreUnspacedFflogsSlugs()
    {
        Assert.Equal("DarkKnight", JobCatalog.Find("Dark Knight")!.SpecName);
        Assert.Equal("WhiteMage", JobCatalog.Find("White Mage")!.SpecName);
        Assert.Equal("BlackMage", JobCatalog.Find("Black Mage")!.SpecName);
        Assert.Equal("RedMage", JobCatalog.Find("Red Mage")!.SpecName);
        Assert.All(JobCatalog.All, job => Assert.DoesNotContain(' ', job.SpecName));
    }
}
