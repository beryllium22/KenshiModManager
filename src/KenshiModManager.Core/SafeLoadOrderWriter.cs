using System.Security.Cryptography;
using System.Text;

namespace KenshiModManager.Core;

public sealed record LoadOrderSnapshot(string Path, byte[] Bytes, string Sha256);
public sealed record ApplyResult(string BackupPath, string Sha256, int EnabledCount);

/// <summary>Explicit, backup-protected writes; never called by profile selection.</summary>
public sealed class SafeLoadOrderWriter
{
    public LoadOrderSnapshot Read(string cfgPath)
    {
        var path = Path.GetFullPath(cfgPath);
        var bytes = File.ReadAllBytes(path);
        return new(path, bytes, Hash(bytes));
    }

    public ApplyResult Apply(string cfgPath, ModProfile profile, IReadOnlyList<InstalledMod> inventory,
        LoadOrderSnapshot expected, string backupDirectory, bool gameRunning = false)
    {
        if (gameRunning) throw new InvalidOperationException("Close Kenshi before changing its load order.");
        var names = profile.Mods.Where(m => m.Enabled).Select(m => m.FileName).ToArray();
        if (names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Length)
            throw new InvalidOperationException("Duplicate enabled filenames are not allowed. Remove the duplicate entries before applying.");
        var byName = inventory.ToLookup(m => m.FileName, StringComparer.OrdinalIgnoreCase);
        foreach (var name in names)
        {
            LoadOrderCodec.ValidateFileName(name);
            var matches = byName[name].ToArray();
            if (matches.Length > 1)
                throw new InvalidOperationException($"Multiple installed mods share the filename {name}. Resolve the duplicate installations before applying; profile source selection alone cannot change Kenshi's resolution.");
            if (matches.Length == 0 || !File.Exists(matches[0].FullPath))
                throw new InvalidOperationException($"Enabled mod is missing: {name}. Install it, rescan, or disable it before applying.");
        }
        return Replace(cfgPath, new UTF8Encoding(false).GetBytes(LoadOrderCodec.Format(names)), expected, backupDirectory, names.Length);
    }

    public ApplyResult Restore(string cfgPath, string backupPath, LoadOrderSnapshot expected,
        string backupDirectory, bool gameRunning = false)
    {
        if (gameRunning) throw new InvalidOperationException("Close Kenshi before restoring its load order.");
        var bytes = File.ReadAllBytes(backupPath);
        // Validate text, but restore exact bytes rather than rewriting encoding/newlines.
        var names = LoadOrderCodec.Parse(new UTF8Encoding(false, true).GetString(bytes));
        if (names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Count)
            throw new InvalidOperationException("Backup contains duplicate mod filenames; restore was not applied.");
        return Replace(cfgPath, bytes, expected, backupDirectory, names.Count);
    }

    private static ApplyResult Replace(string cfgPath, byte[] bytes, LoadOrderSnapshot expected, string backupDirectory, int count)
    {
        var path = Path.GetFullPath(cfgPath);
        var key = Hash(Encoding.UTF8.GetBytes(path.ToUpperInvariant()));
        using var operation = new Mutex(false, "Local\\KenshiModManager-" + key);
        bool acquired;
        try { acquired = operation.WaitOne(0); }
        catch (AbandonedMutexException) { acquired = true; } // Ownership transferred; still validate current bytes.
        if (!acquired) throw new InvalidOperationException("Another manager is applying this load order. Wait for it to finish and reload.");
        try
        {
            ValidateSnapshot(path, expected);
            if ((File.GetAttributes(path) & FileAttributes.ReadOnly) != 0)
                throw new UnauthorizedAccessException("Load-order file is read-only. Fix its permissions before applying.");
            // Keep ordinary external writers out through commit. Delete sharing allows
            // Windows File.Replace to replace the target while this read guard is held.
            using var guard = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            ValidateSnapshot(path, expected);
            Directory.CreateDirectory(backupDirectory);
            var backup = Path.Combine(backupDirectory, $"mods-{DateTime.UtcNow:yyyyMMdd-HHmmss-fffffff}-{Guid.NewGuid():N}.cfg");
            var stage = Path.Combine(Path.GetDirectoryName(path)!, ".mods-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var saved = new FileStream(backup, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                {
                    saved.Write(expected.Bytes);
                    saved.Flush(true);
                }
                using (var staged = new FileStream(stage, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    staged.Write(bytes);
                    staged.Flush(true);
                }
                ValidateSnapshot(path, expected); // Detect an external atomic replacement since preflight.
                File.Replace(stage, path, null);
                var actual = File.ReadAllBytes(path);
                if (!actual.AsSpan().SequenceEqual(bytes))
                    throw new IOException($"Load order verification failed. Restore the exact backup at {backup}.");
                return new(backup, Hash(actual), count);
            }
            finally
            {
                if (File.Exists(stage)) File.Delete(stage);
            }
        }
        finally { operation.ReleaseMutex(); }
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static void ValidateSnapshot(string path, LoadOrderSnapshot expected)
    {
        if (!string.Equals(path, Path.GetFullPath(expected.Path), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Snapshot belongs to another load-order file. Reload the current configuration.");
        if (!string.Equals(Hash(expected.Bytes), expected.Sha256, StringComparison.Ordinal) ||
            !string.Equals(Hash(File.ReadAllBytes(path)), expected.Sha256, StringComparison.Ordinal))
            throw new InvalidOperationException("Kenshi load order changed outside the manager. Reload it before applying.");
    }
}

