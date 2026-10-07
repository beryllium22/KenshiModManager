using KenshiModManager.Core;
using Xunit;
namespace KenshiModManager.Core.Tests;
public sealed class MetadataTests
{
    [Fact]
    public async Task LocalUnpublishedMetadataDoesNotExposeWorkshopLinkZero()
    {
        using var f = new InventoryFixture();
        var folder = f.Folder("mods/Local");
        File.WriteAllText(Path.Combine(folder, "Actual.mod"), "unused");
        File.WriteAllText(Path.Combine(folder, "_Actual.info"), "<ModData><id>0</id><mod>Actual</mod><title>Local title</title></ModData>");
        var mod = Assert.Single(await new ModInventoryScanner().ScanAsync(f.Root, []));
        Assert.Equal("Local title", mod.DisplayName);
        Assert.Null(mod.WorkshopId);
    }

    [Fact]
    public async Task ConflictingSameModSidecarsFallBackRatherThanChooseArbitraryTitle()
    {
        using var f = new InventoryFixture();
        var folder = f.Folder("workshop/123");
        File.WriteAllText(Path.Combine(folder, "Actual.mod"), "unused");
        foreach (var title in new[] { "One", "Two", "Three" })
            File.WriteAllText(Path.Combine(folder, title + ".info"), "<ModData><id>123</id><mod>Actual</mod><title>" + title + "</title></ModData>");
        var scanner = new ModInventoryScanner();
        var mod = Assert.Single(await scanner.ScanAsync(f.Root, [Path.Combine(f.Root, "workshop")]));
        Assert.Equal("Actual.mod", mod.DisplayName);
        Assert.Contains(scanner.Diagnostics, warning => warning.Contains("Duplicate metadata"));
    }

    [Fact]
    public async Task LockedSidecarProducesDiagnosticWithoutDroppingMod()
    {
        using var f = new InventoryFixture();
        var folder = f.Folder("mods/Local");
        File.WriteAllText(Path.Combine(folder, "Actual.mod"), "unused");
        var path = Path.Combine(folder, "_Actual.info");
        File.WriteAllText(path, "<ModData><mod>Actual</mod><title>Locked</title></ModData>");
        using var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var scanner = new ModInventoryScanner();
        var mod = Assert.Single(await scanner.ScanAsync(f.Root, []));
        Assert.Equal("Actual.mod", mod.DisplayName);
        Assert.Contains(scanner.Diagnostics, warning => warning.Contains(path));
    }

    [Fact]
    public async Task WorkshopIdElementMatchesCaseInsensitiveModAndReadsTags()
    {
        using var f = new InventoryFixture();
        var folder = f.Folder("workshop/123");
        File.WriteAllText(Path.Combine(folder, "Actual.MOD"), "unused");
        File.WriteAllText(Path.Combine(folder, "_Actual.info"), "<ModData><WorkshopID>123</WorkshopID><mod>actual.mod</mod><title>Title</title><tags><string> Tag </string><string>tag</string><string /></tags></ModData>");
        var mod = Assert.Single(await new ModInventoryScanner().ScanAsync(f.Root, [Path.Combine(f.Root, "workshop")]));
        Assert.Equal("Title", mod.DisplayName);
        Assert.Equal("Tag", Assert.Single(mod.Tags!));
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("dtd")]
    [InlineData("oversized")]
    [InlineData("wrong-id")]
    public async Task UnsafeOrInvalidSidecarsNeverDropInstalledMods(string kind)
    {
        using var fixture = new InventoryFixture();
        var folder = fixture.Folder("workshop/123");
        File.WriteAllText(Path.Combine(folder, "Actual.mod"), "not parsed");
        var xml = kind switch
        {
            "malformed" => "<ModData><mod>Actual</mod>",
            "dtd" => "<!DOCTYPE ModData [<!ENTITY title 'unsafe'>]><ModData><id>123</id><mod>Actual</mod><title>&title;</title></ModData>",
            "oversized" => "<ModData><id>123</id><mod>Actual</mod><title>" + new string('x', 1024 * 1024) + "</title></ModData>",
            _ => "<ModData><id>999</id><mod>Actual</mod><title>Wrong item</title></ModData>"
        };
        File.WriteAllText(Path.Combine(folder, "_Actual.info"), xml);
        var scanner = new ModInventoryScanner();
        var mod = Assert.Single(await scanner.ScanAsync(fixture.Root, [Path.Combine(fixture.Root, "workshop")]));
        Assert.Equal("Actual.mod", mod.DisplayName);
        Assert.Equal("123", mod.WorkshopId);
        Assert.Contains(scanner.Diagnostics, d => d.Contains("_Actual.info"));
    }

    [Fact]
    public async Task ReadsOnlyMatchingWorkshopMetadataAndPreservesLiteralFilename()
    {
        using var fixture = new InventoryFixture();
        var folder = fixture.Folder("workshop/123");
        File.WriteAllText(Path.Combine(folder, "Actual.mod"), "not parsed");
        File.WriteAllText(Path.Combine(folder, "_stale.info"), "<ModData><id>123</id><mod>Other</mod><title>Wrong</title></ModData>");
        File.WriteAllText(Path.Combine(folder, "_arbitrary.info"), "<ModData><id>123</id><mod>Actual</mod><title>日本 Title</title><tags><string>Buildings</string><string>Gameplay</string></tags></ModData>");
        var mods = await new ModInventoryScanner().ScanAsync(fixture.Root, [Path.Combine(fixture.Root, "workshop")]);
        var mod = Assert.Single(mods);
        Assert.Equal("Actual.mod", mod.FileName);
        Assert.Equal("workshop:123/Actual.mod", mod.Id);
        Assert.Equal("123", mod.WorkshopId);
        Assert.Equal("日本 Title", mod.DisplayName);
        Assert.Equal(new[] { "Buildings", "Gameplay" }, mod.Tags);
        Assert.Equal(ModSource.Workshop, mod.Source);
    }
}
internal sealed class InventoryFixture : IDisposable
{
    public string Root { get; }
    public InventoryFixture()
    {
        var project = new DirectoryInfo(AppContext.BaseDirectory);
        while (project != null && !File.Exists(Path.Combine(project.FullName, ".hermes", "CONTRACT.md"))) project = project.Parent;
        if (project == null) throw new InvalidOperationException("Project root required for managed test data.");
        Root = Path.Combine(project.FullName, ".hermes", "test-data", "inventory-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }
    public string Folder(string relative)
    {
        var path = Path.GetFullPath(Path.Combine(Root, relative));
        Directory.CreateDirectory(path);
        return path;
    }
    public void Dispose() => Directory.Delete(Root, true);
}
