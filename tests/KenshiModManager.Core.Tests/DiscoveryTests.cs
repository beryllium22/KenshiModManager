using KenshiModManager.Core;
using Xunit;
namespace KenshiModManager.Core.Tests;
public sealed class DiscoveryTests
{
    [Theory]
    [InlineData("\"libraryfolders\" { \"0\" { \"path\" \"unterminated")]
    [InlineData("\"libraryfolders\" { \"0\" { \"path\" \"closed\" }")]
    [InlineData("\"libraryfolders\" }")]
    public void MalformedLibraryMetadataRetainsKnownRootAndInferredWorkshop(string vdf)
    {
        using var f = new InventoryFixture();
        var steam = f.Folder("Steam");
        var game = f.Folder("Steam/steamapps/common/Kenshi");
        var workshop = f.Folder("Steam/steamapps/workshop/content/233860");
        File.WriteAllText(Path.Combine(steam, "steamapps", "libraryfolders.vdf"), vdf);
        Assert.Equal(game, Assert.Single(SteamLibraryDiscovery.FindKenshiPaths([steam])));
        Assert.Equal(workshop, Assert.Single(SteamLibraryDiscovery.WorkshopPathsForGame(game, [steam])));
    }

    [Fact]
    public async Task MissingGameAndWorkshopRootsProduceActionableDiagnostics()
    {
        using var f = new InventoryFixture();
        var scanner = new ModInventoryScanner();
        Assert.Empty(await scanner.ScanAsync(Path.Combine(f.Root, "absent"), [Path.Combine(f.Root, "missing")]));
        Assert.Equal(2, scanner.Diagnostics.Count);
        Assert.All(scanner.Diagnostics, warning => Assert.Contains(f.Root, warning));
    }

    [Fact]
    public void FindsModernAndLegacyLibrariesAndInfersWorkshopFromGame()
    {
        using var f = new InventoryFixture();
        var steam = f.Folder("Steam");
        f.Folder("Steam/steamapps");
        var modern = f.Folder("Modern Library");
        var legacy = f.Folder("Legacy Library");
        var primaryGame = f.Folder("Steam/steamapps/common/Kenshi");
        var modernGame = f.Folder("Modern Library/steamapps/common/Kenshi");
        var legacyGame = f.Folder("Legacy Library/steamapps/common/Kenshi");
        var primaryWorkshop = f.Folder("Steam/steamapps/workshop/content/233860");
        var modernWorkshop = f.Folder("Modern Library/steamapps/workshop/content/233860");
        string Escape(string path) => path.Replace("\\", "\\\\");
        File.WriteAllText(Path.Combine(steam, "steamapps", "libraryfolders.vdf"),
            "\"libraryfolders\" { // ignored comment\n \"0\" { \"path\" \"" + Escape(steam) + "\" \"apps\" { \"233860\" \"1\" } } \"1\" { \"path\" \"" + Escape(modern) + "\" } \"2\" \"" + Escape(legacy) + "\" \"description\" \"not-a-library\" }");
        var games = SteamLibraryDiscovery.FindKenshiPaths([steam, steam.ToUpperInvariant()]);
        Assert.Equal(3, games.Count);
        Assert.Contains(primaryGame, games, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(modernGame, games, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(legacyGame, games, StringComparer.OrdinalIgnoreCase);
        var workshops = SteamLibraryDiscovery.WorkshopPathsForGame(modernGame, [steam]);
        Assert.Equal(2, workshops.Count);
        Assert.Contains(primaryWorkshop, workshops, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(modernWorkshop, workshops, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ScansOnlyDirectModFilesAndDeduplicatesWorkshopRoots()
    {
        using var f = new InventoryFixture();
        var local = f.Folder("mods/Local Folder");
        File.WriteAllText(Path.Combine(local, "Local.mod"), "binary unused");
        var nested = f.Folder("mods/Local Folder/textures/deep");
        File.WriteAllText(Path.Combine(nested, "NotAMod.mod"), "payload unused");
        var workshop = f.Folder("workshop/123");
        File.WriteAllText(Path.Combine(workshop, "Workshop.MOD"), "binary unused");
        var root = Path.Combine(f.Root, "workshop");
        var scanner = new ModInventoryScanner();
        var mods = await scanner.ScanAsync(f.Root, [root, root.ToUpperInvariant() + Path.DirectorySeparatorChar, Path.Combine(f.Root, "missing")]);
        Assert.Equal(2, mods.Count);
        var mod = Assert.Single(mods, m => m.Source == ModSource.Local);
        Assert.Equal("local:Local Folder/Local.mod", mod.Id);
        Assert.Equal("Local.mod", mod.DisplayName);
        Assert.Equal("workshop:123/Workshop.MOD", Assert.Single(mods, m => m.Source == ModSource.Workshop).Id);
        Assert.Contains(scanner.Diagnostics, d => d.Contains("missing"));
    }

    [Fact]
    public async Task CancellationDuringRootEnumerationDoesNotReturnPartialInventory()
    {
        using var f = new InventoryFixture();
        f.Folder("workshop/123");
        using var cancellation = new CancellationTokenSource();
        IEnumerable<string> Roots()
        {
            cancellation.Cancel();
            yield return Path.Combine(f.Root, "workshop");
        }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ModInventoryScanner().ScanAsync(f.Root, Roots(), cancellation.Token));
    }

    [Fact]
    public async Task PreCancelledScanDoesNotEnumeratePaths()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        IEnumerable<string> Roots() { yield return Throw(); }
        string Throw() => throw new InvalidOperationException("Must not enumerate");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ModInventoryScanner().ScanAsync("unused", Roots(), cancelled.Token));
    }
}
