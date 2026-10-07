using KenshiModManager.Core;
using System.Security.Cryptography;
using Xunit;

namespace KenshiModManager.Apply.Tests;

public sealed class SafeWriterTests : IDisposable
{
    private readonly string root = TestFixture.CreateRoot("apply");
    public SafeWriterTests() => Directory.CreateDirectory(root);
    public void Dispose() => Directory.Delete(root, true);

    [Fact]
    public void Apply_writes_only_enabled_order_and_keeps_exact_backup()
    {
        var cfg = Path.Combine(root, "mods.cfg");
        byte[] original = [0xEF, 0xBB, 0xBF, .. System.Text.Encoding.UTF8.GetBytes("old.mod\n")];
        File.WriteAllBytes(cfg, original);
        var a = Path.Combine(root, "A.mod"); File.WriteAllBytes(a, []);
        var b = Path.Combine(root, "B.mod"); File.WriteAllBytes(b, []);
        var profile = new ModProfile { Mods = [new("B.mod"), new("missing.mod", false), new("A.mod")] };
        InstalledMod[] inventory = [new("local:A", "A.mod", a, ModSource.Local, "A"), new("local:B", "B.mod", b, ModSource.Local, "B")];
        var writer = new SafeLoadOrderWriter();
        var method = typeof(SafeLoadOrderWriter).GetMethod("Apply");
        Assert.NotNull(method);
        var result = (ApplyResult)method.Invoke(writer, [cfg, profile, inventory, writer.Read(cfg), Path.Combine(root, "backups"), false])!;
        Assert.Equal("B.mod\r\nA.mod\r\n", File.ReadAllText(cfg));
        Assert.Equal(original, File.ReadAllBytes(result.BackupPath));
        Assert.Equal(2, result.EnabledCount);
        Assert.Equal(writer.Read(cfg).Sha256, result.Sha256);
    }

    [Fact]
    public void Apply_rejects_external_change_and_preserves_external_bytes()
    {
        var cfg = Path.Combine(root, "mods.cfg"); File.WriteAllText(cfg, "A.mod\r\n");
        var writer = new SafeLoadOrderWriter(); var expected = writer.Read(cfg);
        File.WriteAllText(cfg, "other.mod\r\n");
        Assert.Throws<InvalidOperationException>(() => writer.Apply(cfg, new ModProfile(), [], expected, Path.Combine(root, "backups")));
        Assert.Equal("other.mod\r\n", File.ReadAllText(cfg));
        Assert.False(Directory.Exists(Path.Combine(root, "backups")));
    }

    [Fact]
    public void Apply_rejects_running_game_before_any_write()
    {
        var cfg = Path.Combine(root, "mods.cfg"); File.WriteAllText(cfg, "A.mod\r\n");
        var writer = new SafeLoadOrderWriter(); var before = writer.Read(cfg);
        Assert.Throws<InvalidOperationException>(() => writer.Apply(cfg, new ModProfile(), [], before, Path.Combine(root, "backups"), true));
        Assert.Equal(before.Bytes, File.ReadAllBytes(cfg));
        Assert.False(Directory.Exists(Path.Combine(root, "backups")));
    }

    [Fact]
    public void Apply_rejects_missing_enabled_mod_and_stale_scanned_file()
    {
        var cfg = Path.Combine(root, "mods.cfg"); File.WriteAllText(cfg, "A.mod\r\n");
        var writer = new SafeLoadOrderWriter(); var before = writer.Read(cfg);
        var profile = new ModProfile { Mods = [new("Missing.mod")] };
        Assert.Throws<InvalidOperationException>(() => writer.Apply(cfg, profile, [], before, Path.Combine(root, "backups")));
        InstalledMod[] stale = [new("local:missing", "Missing.mod", Path.Combine(root, "Missing.mod"), ModSource.Local, "Missing")];
        Assert.Throws<InvalidOperationException>(() => writer.Apply(cfg, profile, stale, before, Path.Combine(root, "backups")));
        Assert.Equal(before.Bytes, File.ReadAllBytes(cfg));
    }

    [Fact]
    public void Apply_rejects_ambiguous_same_filename_sources()
    {
        var cfg = Path.Combine(root, "mods.cfg"); File.WriteAllText(cfg, "A.mod\r\n");
        var mod = Path.Combine(root, "A.mod"); File.WriteAllBytes(mod, []);
        var writer = new SafeLoadOrderWriter(); var before = writer.Read(cfg);
        InstalledMod[] inventory = [new("local:A", "A.mod", mod, ModSource.Local, "A"), new("workshop:1/A.mod", "a.mod", mod, ModSource.Workshop, "A", "1")];
        Assert.Throws<InvalidOperationException>(() => writer.Apply(cfg, new ModProfile { Mods = [new("A.mod")] }, inventory, before, Path.Combine(root, "backups")));
        Assert.Equal(before.Bytes, File.ReadAllBytes(cfg));
    }

    [Fact]
    public void Apply_rejects_duplicate_enabled_names_case_insensitively()
    {
        var cfg = Path.Combine(root, "mods.cfg"); File.WriteAllText(cfg, "old.mod\r\n");
        var mod = Path.Combine(root, "A.mod"); File.WriteAllBytes(mod, []);
        var writer = new SafeLoadOrderWriter(); var before = writer.Read(cfg);
        InstalledMod[] inventory = [new("local:A", "A.mod", mod, ModSource.Local, "A")];
        Assert.Throws<InvalidOperationException>(() => writer.Apply(cfg, new ModProfile { Mods = [new("A.mod"), new("a.mod")] }, inventory, before, Path.Combine(root, "backups")));
        Assert.Equal(before.Bytes, File.ReadAllBytes(cfg));
    }

    [Fact]
    public void Apply_rejects_path_entries_before_inventory_or_file_access()
    {
        var cfg = Path.Combine(root, "mods.cfg"); File.WriteAllText(cfg, "old.mod\r\n");
        var writer = new SafeLoadOrderWriter(); var before = writer.Read(cfg);
        Assert.Throws<FormatException>(() => writer.Apply(cfg, new ModProfile { Mods = [new("../escape.mod")] }, [], before, Path.Combine(root, "backups")));
        Assert.Equal(before.Bytes, File.ReadAllBytes(cfg));
    }

    [Fact]
    public void Apply_read_only_target_is_rejected_before_backup_creation()
    {
        var cfg = Path.Combine(root, "mods.cfg"); File.WriteAllText(cfg, "old.mod\r\n");
        var writer = new SafeLoadOrderWriter(); var before = writer.Read(cfg);
        File.SetAttributes(cfg, FileAttributes.ReadOnly);
        try
        {
            Assert.Throws<UnauthorizedAccessException>(() => writer.Apply(cfg, new ModProfile(), [], before, Path.Combine(root, "backups")));
            Assert.Equal(before.Bytes, File.ReadAllBytes(cfg));
            Assert.False(Directory.Exists(Path.Combine(root, "backups")));
        }
        finally { File.SetAttributes(cfg, FileAttributes.Normal); }
    }

    [Fact]
    public void Restore_keeps_exact_encoding_and_backs_up_current_file()
    {
        var cfg = Path.Combine(root, "mods.cfg"); File.WriteAllText(cfg, "A.mod\r\n");
        var backup = Path.Combine(root, "original.cfg");
        byte[] original = [0xEF, 0xBB, 0xBF, .. System.Text.Encoding.UTF8.GetBytes("old.mod\n")];
        File.WriteAllBytes(backup, original);
        var writer = new SafeLoadOrderWriter(); var before = writer.Read(cfg);
        var method = typeof(SafeLoadOrderWriter).GetMethod("Restore"); Assert.NotNull(method);
        var result = (ApplyResult)method.Invoke(writer, [cfg, backup, before, Path.Combine(root, "backups"), false])!;
        Assert.Equal(original, File.ReadAllBytes(cfg));
        Assert.Equal(before.Bytes, File.ReadAllBytes(result.BackupPath));
        Assert.Equal(writer.Read(cfg).Sha256, result.Sha256);
    }

    [Fact]
    public void Apply_cannot_overwrite_a_configuration_opened_for_external_writing()
    {
        var cfg = Path.Combine(root, "mods.cfg"); File.WriteAllText(cfg, "old.mod\r\n");
        var writer = new SafeLoadOrderWriter(); var before = writer.Read(cfg);
        using (var external = new FileStream(cfg, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
            Assert.Throws<IOException>(() => writer.Apply(cfg, new ModProfile(), [], before, Path.Combine(root, "backups")));
        Assert.Equal(before.Bytes, File.ReadAllBytes(cfg));
        Assert.False(Directory.Exists(Path.Combine(root, "backups")));
    }

    [Fact]
    public void Snapshot_for_another_path_or_mutated_bytes_is_rejected()
    {
        var cfg = Path.Combine(root, "mods.cfg"); File.WriteAllText(cfg, "old.mod\r\n");
        var other = Path.Combine(root, "other.cfg"); File.WriteAllText(other, "old.mod\r\n");
        var writer = new SafeLoadOrderWriter();
        Assert.Throws<InvalidOperationException>(() => writer.Apply(cfg, new ModProfile(), [], writer.Read(other), Path.Combine(root, "backups")));
        var snapshot = writer.Read(cfg); snapshot.Bytes[0] ^= 1;
        Assert.Throws<InvalidOperationException>(() => writer.Apply(cfg, new ModProfile(), [], snapshot, Path.Combine(root, "backups")));
        Assert.Equal("old.mod\r\n", File.ReadAllText(cfg));
    }

    [Fact]
    public void Restore_running_game_external_change_or_invalid_backup_is_rejected()
    {
        var cfg = Path.Combine(root, "mods.cfg"); File.WriteAllText(cfg, "old.mod\r\n");
        var backup = Path.Combine(root, "valid.cfg"); File.WriteAllText(backup, "A.mod\r\n");
        var writer = new SafeLoadOrderWriter(); var before = writer.Read(cfg);
        Assert.Throws<InvalidOperationException>(() => writer.Restore(cfg, backup, before, Path.Combine(root, "backups"), true));
        File.WriteAllText(cfg, "external.mod\r\n");
        Assert.Throws<InvalidOperationException>(() => writer.Restore(cfg, backup, before, Path.Combine(root, "backups")));
        File.WriteAllText(backup, "../escape.mod\r\n");
        Assert.Throws<FormatException>(() => writer.Restore(cfg, backup, writer.Read(cfg), Path.Combine(root, "backups")));
        Assert.Equal("external.mod\r\n", File.ReadAllText(cfg));
    }

    [Fact]
    public void Blocked_backup_location_keeps_original_cfg_and_leaves_no_staging_file()
    {
        var cfg = Path.Combine(root, "mods.cfg"); File.WriteAllText(cfg, "old.mod\r\n");
        var blocked = Path.Combine(root, "blocked"); File.WriteAllText(blocked, "not a directory");
        var writer = new SafeLoadOrderWriter(); var before = writer.Read(cfg);
        Assert.Throws<IOException>(() => writer.Apply(cfg, new ModProfile(), [], before, blocked));
        Assert.Equal(before.Bytes, File.ReadAllBytes(cfg));
        Assert.Empty(Directory.GetFiles(root, ".mods-*.tmp"));
    }

    [Fact]
    public void Empty_profile_produces_empty_cfg_and_successive_backups_are_unique()
    {
        var cfg = Path.Combine(root, "mods.cfg"); File.WriteAllText(cfg, "old.mod\r\n");
        var writer = new SafeLoadOrderWriter();
        var one = writer.Apply(cfg, new ModProfile(), [], writer.Read(cfg), Path.Combine(root, "backups"));
        var two = writer.Apply(cfg, new ModProfile(), [], writer.Read(cfg), Path.Combine(root, "backups"));
        Assert.NotEqual(one.BackupPath, two.BackupPath);
        Assert.Empty(File.ReadAllBytes(cfg));
        Assert.Equal(0, two.EnabledCount);
        Assert.Equal("old.mod\r\n", File.ReadAllText(one.BackupPath));
    }

    [Fact]
    public async Task Concurrent_manager_apply_is_rejected_before_any_write()
    {
        var cfg = Path.Combine(root, "mods.cfg"); File.WriteAllText(cfg, "old.mod\r\n");
        var writer = new SafeLoadOrderWriter(); var before = writer.Read(cfg);
        var key = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(cfg).ToUpperInvariant())));
        using var ready = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        var holder = Task.Run(() =>
        {
            using var mutex = new Mutex(false, "Local\\KenshiModManager-" + key);
            mutex.WaitOne(); ready.Set(); release.Wait(); mutex.ReleaseMutex();
        });
        Assert.True(ready.Wait(TimeSpan.FromSeconds(5)));
        try { Assert.Throws<InvalidOperationException>(() => writer.Apply(cfg, new ModProfile(), [], before, Path.Combine(root, "backups"))); }
        finally { release.Set(); await holder; }
        Assert.Equal(before.Bytes, File.ReadAllBytes(cfg));
    }

    [Fact]
    public void Read_preserves_exact_bytes_and_computes_snapshot_hash()
    {
        var file = Path.Combine(root, "mods.cfg");
        byte[] bytes = [0xEF, 0xBB, 0xBF, .. System.Text.Encoding.UTF8.GetBytes("Oni.mod\r\n")];
        File.WriteAllBytes(file, bytes);
        var type = System.Reflection.Assembly.Load("KenshiModManager.Core").GetType("KenshiModManager.Core.SafeLoadOrderWriter");
        Assert.NotNull(type);
        var writer = Activator.CreateInstance(type);
        var result = type.GetMethod("Read")!.Invoke(writer, [file])!;
        Assert.Equal(bytes, (byte[])result.GetType().GetProperty("Bytes")!.GetValue(result)!);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)), (string)result.GetType().GetProperty("Sha256")!.GetValue(result)!);
    }
}
