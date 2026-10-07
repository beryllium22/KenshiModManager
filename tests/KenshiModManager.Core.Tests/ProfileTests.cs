using KenshiModManager.Core;
using Xunit;
namespace KenshiModManager.Core.Tests;
public class ProfileTests
{
    [Fact]
    public void DuplicateHasIndependentIdentityListAndMetadata()
    {
        var original = ProfileStore.Create([], "Set", ["A.mod"]);
        original.SourceUrl = "https://steamcommunity.com/sharedfiles/filedetails/?id=123";
        original.Notes = "order";
        original.Mods.Add(new ProfileMod("B.mod", false, "workshop:123/B.mod"));
        var copy = ProfileStore.Duplicate([original], original, " Copy ");
        Assert.NotEqual(original.Id, copy.Id);
        Assert.Equal("Copy", copy.Name);
        Assert.Equal(original.Mods, copy.Mods);
        Assert.NotSame(original.Mods, copy.Mods);
        Assert.NotSame(original.Mods[0], copy.Mods[0]);
        Assert.Equal(original.SourceUrl, copy.SourceUrl);
        Assert.Equal(original.Notes, copy.Notes);
        copy.Mods.Clear();
        Assert.Equal(2, original.Mods.Count);
        Assert.Equal("Profile 1", ProfileStore.Duplicate([original], original).Name);
    }

    [Fact]
    public void CreateUsesFirstAvailableDefaultAndPreservesMissingNames()
    {
        var profile = ProfileStore.Create([new ModProfile { Name = "Profile 1" }, new ModProfile { Name = "Profile 3" }], "   ", ["Missing.mod", "日本.mod"]);
        Assert.Equal("Profile 2", profile.Name);
        Assert.Equal(new[] { "Missing.mod", "日本.mod" }, profile.Mods.Select(m => m.FileName));
        Assert.All(profile.Mods, m => Assert.True(m.Enabled));
        Assert.Equal("Named", ProfileStore.Create([], " Named ", []).Name);
    }
}
