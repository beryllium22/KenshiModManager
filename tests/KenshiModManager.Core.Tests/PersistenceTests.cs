using KenshiModManager.Core;
using Xunit;
namespace KenshiModManager.Core.Tests;
public sealed class FixtureDirectory : IDisposable
{
    public string Root { get; }
    public FixtureDirectory()
    {
        var project = new DirectoryInfo(AppContext.BaseDirectory);
        while (project is not null && !File.Exists(Path.Combine(project.FullName, ".hermes", "CONTRACT.md"))) project = project.Parent;
        if (project is null) throw new InvalidOperationException("Tests require the project test-data directory.");
        Root = Path.Combine(project.FullName, ".hermes", "test-data", "core-" + Guid.NewGuid());
        Directory.CreateDirectory(Root);
    }
    public string PathFor(string relative) => Path.Combine(Root, relative);
    public void Dispose() => Directory.Delete(Root, true);
}
public class PersistenceTests
{
    [Fact]
    public void StateRoundTripsSelectionsPathsAndIndependentProfilesAtomically()
    {
        using var fixture = new FixtureDirectory();
        var path = fixture.PathFor("state.json");
        var store = new ProfileStore(path);
        Assert.Empty(store.Load().Profiles);
        var profile = ProfileStore.Create([], "Current Kenshi Setup", ["Missing.mod"]);
        profile.Mods.Add(new ProfileMod("Disabled.mod", false, "local:folder/Disabled.mod"));
        var state = new ManagerState { GamePath = "D:\\Games\\Kenshi", WorkshopPaths = ["D:\\Steam\\steamapps\\workshop\\content\\233860"], Profiles = [profile], SelectedProfileId = profile.Id, AppliedProfileId = profile.Id };
        store.Save(state);
        var loaded = store.Load();
        Assert.Equal(state.GamePath, loaded.GamePath);
        Assert.Equal(state.WorkshopPaths, loaded.WorkshopPaths);
        Assert.Equal(profile.Id, loaded.SelectedProfileId);
        Assert.Equal(profile.Id, loaded.AppliedProfileId);
        Assert.Equal(profile.Mods, loaded.Profiles[0].Mods);
        loaded.Profiles[0].Name = "Changed";
        Assert.Equal("Current Kenshi Setup", profile.Name);
        store.Save(loaded);
        Assert.Equal("Changed", store.Load().Profiles[0].Name);
        Assert.Single(Directory.GetFiles(fixture.Root));
        Assert.False(File.ReadAllBytes(path).AsSpan().StartsWith(new byte[] { 239, 187, 191 }));
    }
}
