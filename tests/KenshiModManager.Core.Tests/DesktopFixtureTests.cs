using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;
using KenshiModManager.Core;
using Xunit;

namespace KenshiModManager.Core.Tests;

public sealed class DesktopFixtureTests
{
    private static readonly string[] Names =
    [
        "Fixture UI.mod", "Fixture World.mod", "Fixture Animation.mod",
        "Fixture Patch.mod", "Fixture Local.mod", "日本語 Fixture.mod"
    ];

    [Fact]
    public async Task ActualGeneratorProducesSixInstalledModsWithHydratedSyntheticMetadata()
    {
        using var fixture = new SyntheticCheckout();
        await fixture.Generate();
        var scanner = new ModInventoryScanner();
        var installed = await scanner.ScanAsync(fixture.Game, [fixture.Workshop]);

        Assert.Equal(6, installed.Count);
        Assert.Equal(Names.Order(StringComparer.Ordinal), installed.Select(mod => mod.FileName).Order(StringComparer.Ordinal));
        for (var index = 0; index < Names.Length; index++)
        {
            var name = Names[index];
            var stem = Path.GetFileNameWithoutExtension(name);
            var mod = Assert.Single(installed, entry => entry.FileName == name);
            var local = index == 4;
            var workshopId = local ? null : (9000000000L + index).ToString(System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(stem, mod.DisplayName);
            Assert.Equal(new[] { "Test fixture" }, mod.Tags);
            Assert.Equal(workshopId, mod.WorkshopId);
            Assert.Equal(local ? ModSource.Local : ModSource.Workshop, mod.Source);
            Assert.Equal(local ? $"local:{stem}/{name}" : $"workshop:{workshopId}/{name}", mod.Id);
            Assert.Equal(local ? Path.Combine(fixture.Game, "mods", stem, name) : Path.Combine(fixture.Workshop, workshopId!, name), mod.FullPath);
            Assert.Empty(File.ReadAllBytes(mod.FullPath)); // Deliberately not playable mod data.

            var metadata = XDocument.Load(Path.ChangeExtension(mod.FullPath, ".info")).Root!;
            Assert.Equal("ModData", metadata.Name.LocalName);
            Assert.Equal(name, metadata.Element("mod")?.Value);
            Assert.Equal(stem, metadata.Element("title")?.Value);
            Assert.Equal(workshopId ?? "", (metadata.Element("WorkshopID") ?? metadata.Element("id"))?.Value);
            Assert.Equal(new[] { "Test fixture" }, metadata.Element("tags")!.Elements().Select(tag => tag.Value));
        }
        Assert.Empty(scanner.Diagnostics);
        Assert.Equal(Names.Take(4), LoadOrderCodec.Parse(File.ReadAllText(fixture.Cfg)));
        Assert.Equal(new[] { Names[1], Names[0], Names[3], Names[4] }, LoadOrderCodec.Parse(File.ReadAllText(Path.Combine(fixture.Base, "author-load-order.txt"))));
    }

    [Fact]
    public async Task RerunningActualGeneratorPreservesCfgBytesAndSchemaOneManagerState()
    {
        using var fixture = new SyntheticCheckout();
        await fixture.Generate();
        var cfgBytes = new byte[] { 0xEF, 0xBB, 0xBF }
            .Concat(System.Text.Encoding.UTF8.GetBytes("日本語 Fixture.mod\nFixture Local.mod\r\n")).ToArray();
        File.WriteAllBytes(fixture.Cfg, cfgBytes);
        var profile = new ModProfile
        {
            Name = "Synthetic preserved profile",
            Notes = "Keep disabled positions and literal Unicode names.",
            SourceUrl = "https://example.invalid/synthetic-fixture",
            Mods =
            [
                new ProfileMod(Names[5], false, "workshop:9000000005/日本語 Fixture.mod"),
                new ProfileMod(Names[4], true, "local:Fixture Local/Fixture Local.mod")
            ]
        };
        var statePath = Path.Combine(fixture.Base, "manager-data", "state.json");
        var store = new ProfileStore(statePath);
        store.Save(new ManagerState
        {
            SchemaVersion = 1, GamePath = fixture.Game, WorkshopPaths = [fixture.Workshop],
            Profiles = [profile], SelectedProfileId = profile.Id, AppliedProfileId = profile.Id
        });
        var stateBytes = File.ReadAllBytes(statePath);
        var backupPath = Path.Combine(fixture.Base, "manager-data", "backups", "synthetic.cfg");
        Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
        File.WriteAllBytes(backupPath, cfgBytes);

        await fixture.Generate();

        Assert.Equal(cfgBytes, File.ReadAllBytes(fixture.Cfg));
        Assert.Equal(stateBytes, File.ReadAllBytes(statePath));
        Assert.Equal(cfgBytes, File.ReadAllBytes(backupPath));
        Assert.Equal(new[] { backupPath, statePath }.Order(StringComparer.Ordinal),
            Directory.GetFiles(Path.Combine(fixture.Base, "manager-data"), "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal));
        var loaded = store.Load();
        Assert.Equal(1, loaded.SchemaVersion);
        Assert.Equal(profile.Id, loaded.SelectedProfileId);
        Assert.Equal(profile.Id, loaded.AppliedProfileId);
        Assert.Equal(profile.Mods, Assert.Single(loaded.Profiles).Mods);
        using var summary = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixture.Base, "fixture.json")));
        Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(cfgBytes)).ToLowerInvariant(),
            summary.RootElement.GetProperty("cfg_sha256").GetString());
        var scanner = new ModInventoryScanner();
        Assert.Equal(6, (await scanner.ScanAsync(fixture.Game, [fixture.Workshop])).Count);
        Assert.Empty(scanner.Diagnostics);
    }

    private sealed class SyntheticCheckout : IDisposable
    {
        public string Root { get; }
        public string Base => Path.Combine(Root, ".hermes", "test-data", "desktop-fixture");
        public string Game => Path.Combine(Base, "SteamLibrary", "steamapps", "common", "Kenshi");
        public string Workshop => Path.Combine(Base, "SteamLibrary", "steamapps", "workshop", "content", "233860");
        public string Cfg => Path.Combine(Game, "data", "mods.cfg");

        public SyntheticCheckout()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Directory.Build.props")))
                directory = directory.Parent;
            var checkout = directory?.FullName ?? throw new InvalidOperationException("Cannot locate checkout.");
            Root = Path.Combine(checkout, ".hermes", "test-data", "desktop-generator-checkout-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(Root, "scripts"));
            // Exercise the real generator while preserving its parents[1] root convention.
            File.Copy(Path.Combine(checkout, "scripts", "create-fixture.py"), Path.Combine(Root, "scripts", "create-fixture.py"));
        }

        public async Task Generate()
        {
            // A normal Python 3 on PATH is the only Python prerequisite, including in clean clones.
            var start = new ProcessStartInfo("python")
            {
                WorkingDirectory = Root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            start.ArgumentList.Add(Path.Combine(Root, "scripts", "create-fixture.py"));
            start.Environment["PYTHONUTF8"] = "1";
            start.Environment["PYTHONDONTWRITEBYTECODE"] = "1";
            var scratch = Path.Combine(Root, "scratch");
            Directory.CreateDirectory(scratch);
            foreach (var variable in new[] { "TMPDIR", "TMP", "TEMP" }) start.Environment[variable] = scratch;
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Python generator did not start.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { process.Kill(entireProcessTree: true); throw; }
            var output = await stdout;
            var error = await stderr;
            Assert.True(process.ExitCode == 0, $"Generator exited {process.ExitCode}: {error}\n{output}");
            Assert.Equal("", error);
            using var summary = JsonDocument.Parse(output);
            Assert.Equal(Game, summary.RootElement.GetProperty("game_path").GetString());
            Assert.Equal(Workshop, summary.RootElement.GetProperty("workshop_path").GetString());
            Assert.Equal(Path.Combine(Base, "manager-data"), summary.RootElement.GetProperty("data_dir").GetString());
            Assert.Equal(6, summary.RootElement.GetProperty("installed_fixture_count").GetInt32());
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
