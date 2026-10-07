using KenshiModManager.Core;
using System.Text.Json;
using Xunit;
namespace KenshiModManager.Core.Tests;
public class SharingTests
{
    private static string Export(ModProfile profile) => ProfileStore.ExportProfile(profile);
    private static ModProfile Import(string json, IEnumerable<ModProfile> existing) => ProfileStore.ImportProfile(json, existing);

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"schemaVersion\":2,\"profile\":{\"name\":\"Set\",\"mods\":[]}}")]
    [InlineData("{\"schemaVersion\":1,\"profile\":null}")]
    [InlineData("{\"schemaVersion\":1,\"profile\":{\"name\":\"Set\",\"mods\":null}}")]
    [InlineData("{\"schemaVersion\":1,\"profile\":{\"name\":\" \",\"mods\":[]}}")]
    [InlineData("{\"schemaVersion\":1,\"profile\":{\"name\":\"Set\",\"mods\":[null]}}")]
    [InlineData("{\"schemaVersion\":1,\"profile\":{\"name\":\"Set\",\"mods\":[{\"fileName\":\"../A.mod\",\"enabled\":true}]}}")]
    [InlineData("{\"schemaVersion\":1,\"profile\":{\"name\":\"Set\",\"mods\":[{\"fileName\":\"A.mod\",\"enabled\":true},{\"fileName\":\"a.MOD\",\"enabled\":false}]}}")]
    public void ImportRejectsMalformedUnsupportedOrInvalidProfileWithoutMutatingExisting(string json)
    {
        var original = ProfileStore.Create([], "Original", ["Keep.mod"]);
        var error = Assert.Throws<InvalidDataException>(() => Import(json, [original]));
        Assert.Contains("profile", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Original", original.Name);
        Assert.Equal("Keep.mod", Assert.Single(original.Mods).FileName);
    }

    [Theory]
    [InlineData("C:/Games/A.mod")]
    [InlineData("local:folder/A.mod")]
    [InlineData("workshop:C:/Games/A.mod")]
    [InlineData("workshop:123/../A.mod")]
    [InlineData("workshop:abc/A.mod")]
    [InlineData("workshop:123/B.mod")]
    public void ImportRejectsNonportableOrMismatchedInstalledReferences(string reference)
    {
        var json = JsonSerializer.Serialize(new { schemaVersion = 1, profile = new { name = "Set", mods = new[] { new { fileName = "A.mod", enabled = true, installedId = reference } } } });
        Assert.Throws<InvalidDataException>(() => Import(json, []));
    }

    [Theory]
    [InlineData("file:///C:/Games/Kenshi")]
    [InlineData("C:/Games/Kenshi")]
    public void SharingRejectsMachineLocalSourceUrls(string source)
    {
        var profile = ProfileStore.Create([], "Set", ["A.mod"]);
        profile.SourceUrl = source;
        Assert.Throws<InvalidDataException>(() => Export(profile));
        var json = JsonSerializer.Serialize(new { schemaVersion = 1, profile = new { name = "Set", mods = Array.Empty<object>(), sourceUrl = source } });
        Assert.Throws<InvalidDataException>(() => Import(json, []));
    }

    [Fact]
    public void ExportRejectsWorkshopReferencesContainingPaths()
    {
        var profile = ProfileStore.Create([], "Set", ["A.mod"]);
        profile.Mods[0] = new ProfileMod("A.mod", true, "workshop:123/C:/Games/A.mod");
        Assert.Throws<InvalidDataException>(() => Export(profile));
    }

    [Theory]
    [InlineData("{\"schemaVersion\":1,\"profile\":{\"name\":\"Set\",\"mods\":[{\"fileName\":\"A.mod\"}]}}")]
    [InlineData("{\"schemaVersion\":1,\"profile\":{\"name\":\"Set\",\"mods\":[],\"gamePath\":\"C:/Kenshi\"}}")]
    [InlineData("{\"schemaVersion\":2,\"schemaVersion\":1,\"profile\":{\"name\":\"Set\",\"mods\":[]}}")]
    public void ImportRejectsIncompleteOrAmbiguousSharingDocuments(string json)
    {
        Assert.Throws<InvalidDataException>(() => Import(json, []));
    }

    [Fact]
    public void LocalReferencesAreOmittedAndEmptyProfilesRemainPortable()
    {
        var original = ProfileStore.Create([], " Set ", ["A.mod"]);
        original.Mods[0] = new ProfileMod("A.mod", false, "local:folder/A.mod");
        var json = Export(original);
        Assert.DoesNotContain("local:", json);
        Assert.Null(Assert.Single(Import(json, []).Mods).InstalledId);
        var empty = ProfileStore.Create([], "Empty", []);
        Assert.Empty(Import(Export(empty), [empty]).Mods);
        Assert.Equal("Set", Import(json, []).Name);
    }

    [Fact]
    public void PortableProfileRoundTripPreservesExactOrderEnabledAndWorkshopReferencesWithFreshIdentity()
    {
        var profile = ProfileStore.Create([], "Portable", ["日本.mod", "Missing.MOD"]);
        profile.Mods[0] = new ProfileMod("日本.mod", false, "workshop:123/日本.mod");
        profile.Mods[1] = new ProfileMod("Missing.MOD", true, @"C:\Games\Kenshi\mods\Missing.MOD");
        profile.SourceUrl = "https://steamcommunity.com/sharedfiles/filedetails/?id=123";
        profile.Notes = "Author recommends this exact order.";
        var json = Export(profile);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(1, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.DoesNotContain(profile.Id, json);
        Assert.DoesNotContain("C:", json);
        var imported = Import(json, [profile]);
        var again = Import(json, [profile, imported]);
        Assert.NotEqual(profile.Id, imported.Id);
        Assert.NotEqual(imported.Id, again.Id);
        Assert.Equal(profile.Name, imported.Name);
        Assert.Equal(profile.Mods.Select(m => (m.FileName, m.Enabled)), imported.Mods.Select(m => (m.FileName, m.Enabled)));
        Assert.Equal("workshop:123/日本.mod", imported.Mods[0].InstalledId);
        Assert.Null(imported.Mods[1].InstalledId);
        Assert.Equal(profile.SourceUrl, imported.SourceUrl);
        Assert.Equal(profile.Notes, imported.Notes);
        Assert.NotSame(profile.Mods, imported.Mods);
        Assert.NotSame(imported.Mods, again.Mods);
        imported.Mods.Clear();
        Assert.Equal(2, profile.Mods.Count);
        Assert.Equal(2, again.Mods.Count);
    }
}
