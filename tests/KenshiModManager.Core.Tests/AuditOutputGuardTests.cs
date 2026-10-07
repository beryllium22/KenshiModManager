using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Xunit;

namespace KenshiModManager.Core.Tests;

public class AuditOutputGuardTests
{
    [Theory]
    [InlineData("absolute")]
    [InlineData("case")]
    [InlineData("relative")]
    public async Task CfgCollisionIsRejectedBeforeScratchCreationWithoutChangingBytes(string variant)
    {
        using var fixture = new AuditFixture();
        var output = variant switch
        {
            "case" => fixture.Cfg.ToUpperInvariant(),
            "relative" => Path.Combine(Path.GetRelativePath(fixture.Root, fixture.Game), "data", "..", "data", "mods.cfg"),
            _ => fixture.Cfg
        };
        var result = await fixture.Run(output);
        Assert.Equal(fixture.OriginalBytes, File.ReadAllBytes(fixture.Cfg));
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("OUTPUT_JSON", result.Error);
        Assert.False(Directory.Exists(Path.Combine(fixture.Game, "data", "test-data")));
        Assert.Equal(new[] { fixture.Cfg }, Directory.GetFiles(fixture.Root, "*", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData("existing-file")]
    [InlineData("hardlink")]
    [InlineData("symlink")]
    public async Task ExistingOutputIncludingCfgAliasesIsRejectedBeforeScratchCreation(string kind)
    {
        using var fixture = new AuditFixture();
        var output = Path.Combine(fixture.Root, "report.json");
        switch (kind)
        {
            case "hardlink":
                Assert.True(CreateHardLink(output, fixture.Cfg, IntPtr.Zero),
                    $"Synthetic hardlink creation failed: Win32 {Marshal.GetLastWin32Error()}");
                break;
            case "symlink":
                File.CreateSymbolicLink(output, fixture.Cfg);
                break;
            default:
                File.WriteAllBytes(output, fixture.OriginalBytes);
                break;
        }
        var result = await fixture.Run(output);
        Assert.Equal(fixture.OriginalBytes, File.ReadAllBytes(fixture.Cfg));
        Assert.Equal(fixture.OriginalBytes, File.ReadAllBytes(output));
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("OUTPUT_JSON", result.Error);
        Assert.False(Directory.Exists(Path.Combine(fixture.Root, "test-data")));
        Assert.Equal(2, Directory.GetFiles(fixture.Root, "*", SearchOption.AllDirectories).Length);
    }

    [Fact]
    public async Task FreshExternalReportSucceedsWithoutChangingCfgBytes()
    {
        using var fixture = new AuditFixture();
        var output = Path.Combine(fixture.Root, "reports", "fresh.json");
        var result = await fixture.Run(output);
        Assert.True(result.ExitCode == 0, result.Error);
        Assert.Equal(fixture.OriginalBytes, File.ReadAllBytes(fixture.Cfg));
        using var report = JsonDocument.Parse(File.ReadAllText(output));
        Assert.True(report.RootElement.GetProperty("real_cfg_unchanged").GetBoolean());
        Assert.True(report.RootElement.GetProperty("fixture_apply_verified").GetBoolean());
        Assert.True(report.RootElement.GetProperty("fixture_restore_exact_bytes").GetBoolean());
        Assert.Equal(1, report.RootElement.GetProperty("load_order_entries").GetInt32());
        Assert.Empty(Directory.GetDirectories(Path.Combine(fixture.Root, "reports", "test-data")));
        Assert.Equal(2, Directory.GetFiles(fixture.Root, "*", SearchOption.AllDirectories).Length);
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string fileName, string existingFileName, IntPtr securityAttributes);

    private sealed class AuditFixture : IDisposable
    {
        private readonly string checkout;
        public string Root { get; }
        public string Game { get; }
        public string Cfg { get; }
        public byte[] OriginalBytes { get; } = Encoding.UTF8.GetBytes("FixtureAlpha.mod\r\n");

        public AuditFixture()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Directory.Build.props")))
                directory = directory.Parent;
            checkout = directory?.FullName ?? throw new InvalidOperationException("Cannot locate checkout.");
            Root = Path.Combine(checkout, ".hermes", "test-data", "audit-output-guard-" + Guid.NewGuid().ToString("N"));
            Game = Path.Combine(Root, "game");
            Cfg = Path.Combine(Game, "data", "mods.cfg");
            Directory.CreateDirectory(Path.GetDirectoryName(Cfg)!);
            File.WriteAllBytes(Cfg, OriginalBytes);
        }

        public async Task<(int ExitCode, string Output, string Error)> Run(string output)
        {
            var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
            var dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
            if (string.IsNullOrWhiteSpace(dotnet) || !File.Exists(dotnet))
                dotnet = Path.GetFullPath(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", "..", "dotnet.exe"));
            var start = new ProcessStartInfo(dotnet)
            {
                WorkingDirectory = Root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            start.ArgumentList.Add(Path.Combine(checkout, "tools", "KenshiModManager.Audit", "bin", configuration, "net10.0", "KenshiModManager.Audit.dll"));
            start.ArgumentList.Add(Game);
            start.ArgumentList.Add(output);
            start.ArgumentList.Add(Path.Combine(Root, "empty-workshop"));
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Audit process did not start.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { process.Kill(entireProcessTree: true); throw; }
            return (process.ExitCode, await stdout, await stderr);
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
