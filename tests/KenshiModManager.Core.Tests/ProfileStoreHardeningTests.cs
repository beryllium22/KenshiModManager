using KenshiModManager.Core;
using System.Text.Json;
using Xunit;
namespace KenshiModManager.Core.Tests;
public class ProfileStoreHardeningTests
{
    [Fact]
    public void CreateAndDuplicateRejectDuplicateModsAndInvalidCustomNames()
    {
        Assert.Throws<InvalidDataException>(() => ProfileStore.Create([], "Custom", ["A.mod", "a.mod"]));
        Assert.Throws<InvalidDataException>(() => ProfileStore.Create([], "Bad\nName", []));
        var invalid = new ModProfile { Mods = [new ProfileMod("A.mod"), new ProfileMod("a.mod")] };
        Assert.Throws<InvalidDataException>(() => ProfileStore.Duplicate([], invalid));
        var first = ProfileStore.Create([], "Same", ["A.mod"]);
        var second = ProfileStore.Create([first], " Same ", ["B.mod"]);
        Assert.Equal(first.Name, second.Name);
        Assert.NotEqual(first.Id, second.Id);
        second.Mods.Clear();
        Assert.Single(first.Mods);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"profiles\":[],\"workshopPaths\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"profiles\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"workshopPaths\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"profiles\":[{\"name\":\"Set\",\"mods\":[]}],\"workshopPaths\":[]}")]
    [InlineData("{\"schemaVersion\":2,\"schemaVersion\":1,\"profiles\":[],\"workshopPaths\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"profiles\":[],\"workshopPaths\":[],\"unknownData\":123}")]
    public void IncompleteOrAmbiguousDocumentsAreNotResetByDefaults(string json)
    {
        using var fixture = new FixtureDirectory();
        var path = fixture.PathFor("state.json");
        File.WriteAllText(path, json);
        var store = new ProfileStore(path);
        Assert.Throws<InvalidDataException>(() => store.Load());
        Assert.Throws<InvalidDataException>(() => store.Save(new ManagerState()));
        Assert.Equal(json, File.ReadAllText(path));
    }

    [Fact]
    public void BlockedAtomicReplacementPreservesOldStateAndCleansStaging()
    {
        using var fixture = new FixtureDirectory();
        var path = fixture.PathFor("state.json");
        var store = new ProfileStore(path);
        store.Save(new ManagerState());
        var originalBytes = File.ReadAllBytes(path);
        var changed = new ManagerState { Profiles = [ProfileStore.Create([], "New", ["A.mod"])] };
        using (var heldOpen = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.ThrowsAny<IOException>(() => store.Save(changed));
        Assert.Equal(originalBytes, File.ReadAllBytes(path));
        Assert.Empty(store.Load().Profiles);
        Assert.Single(Directory.GetFiles(fixture.Root));
        store.Save(changed);
        Assert.Equal("New", Assert.Single(store.Load().Profiles).Name);
        Assert.Single(Directory.GetFiles(fixture.Root));
    }

    public static IEnumerable<object[]> InvalidStates()
    {
        yield return [new ManagerState { SchemaVersion = 0 }];
        yield return [new ManagerState { SchemaVersion = 2 }];
        yield return [new ManagerState { Profiles = null! }];
        yield return [new ManagerState { WorkshopPaths = null! }];
        yield return [new ManagerState { WorkshopPaths = [null!] }];
        yield return [new ManagerState { Profiles = [null!] }];
        yield return [new ManagerState { Profiles = [new ModProfile { Mods = null! }] }];
        yield return [new ManagerState { Profiles = [new ModProfile { Name = " " }] }];
        yield return [new ManagerState { Profiles = [new ModProfile { Id = "not-a-guid" }] }];
        var id = Guid.NewGuid().ToString();
        yield return [new ManagerState { Profiles = [new ModProfile { Id = id }, new ModProfile { Id = id.ToUpperInvariant() }] }];
        yield return [new ManagerState { SelectedProfileId = Guid.NewGuid().ToString() }];
        yield return [new ManagerState { AppliedProfileId = Guid.NewGuid().ToString() }];
        yield return [new ManagerState { Profiles = [new ModProfile { Mods = [new ProfileMod("../A.mod")] }] }];
        yield return [new ManagerState { Profiles = [new ModProfile { Mods = [new ProfileMod("A.mod"), new ProfileMod("a.MOD", false)] }] }];
        yield return [new ManagerState { Profiles = [new ModProfile { Mods = [null!] }] }];
    }

    [Theory]
    [MemberData(nameof(InvalidStates))]
    public void InvalidStateIsRejectedOnBothLoadAndSaveWithoutReplacingBytes(ManagerState invalid)
    {
        using var fixture = new FixtureDirectory();
        var path = fixture.PathFor("state.json");
        var store = new ProfileStore(path);
        store.Save(new ManagerState());
        var validBytes = File.ReadAllBytes(path);
        Assert.Throws<InvalidDataException>(() => store.Save(invalid));
        Assert.Equal(validBytes, File.ReadAllBytes(path));
        var invalidJson = JsonSerializer.Serialize(invalid, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        File.WriteAllText(path, invalidJson);
        Assert.Throws<InvalidDataException>(() => store.Load());
        Assert.Throws<InvalidDataException>(() => store.Save(new ManagerState()));
        Assert.Equal(invalidJson, File.ReadAllText(path));
        Assert.Single(Directory.GetFiles(fixture.Root));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    public void CorruptStateIsActionableAndCannotBeOverwritten(string json)
    {
        using var fixture = new FixtureDirectory();
        var path = fixture.PathFor("state.json");
        File.WriteAllText(path, json);
        var store = new ProfileStore(path);
        var error = Assert.Throws<InvalidDataException>(() => store.Load());
        Assert.Contains(path, error.Message);
        Assert.Contains("backup", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Throws<InvalidDataException>(() => store.Save(new ManagerState()));
        Assert.Equal(json, File.ReadAllText(path));
        Assert.Single(Directory.GetFiles(fixture.Root));
    }
}
