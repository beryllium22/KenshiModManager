using System.Diagnostics;
using System.Xml;
using System.Xml.Linq;
namespace KenshiModManager.Core;

public sealed class ModInventoryScanner
{
    private IReadOnlyList<string> diagnostics = Array.Empty<string>();
    /// <summary>Warnings from the most recently completed scan. Invalid metadata never removes a mod.</summary>
    public IReadOnlyList<string> Diagnostics => Volatile.Read(ref diagnostics);
    private const int MetadataByteLimit = 1024 * 1024;

    public Task<IReadOnlyList<InstalledMod>> ScanAsync(string gamePath, IEnumerable<string> workshopPaths, CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<InstalledMod>>(() =>
        {
            var warnings = new List<string>();
            try
            {
                var mods = new List<InstalledMod>();
                cancellationToken.ThrowIfCancellationRequested();
                ArgumentException.ThrowIfNullOrWhiteSpace(gamePath);
                ArgumentNullException.ThrowIfNull(workshopPaths);
                var visitedRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var visitedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                void ScanRoot(string root, ModSource source)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string fullRoot;
                    try { fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)); }
                    catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException)
                    { Warn(warnings, root, ex.Message); return; }
                    if (!visitedRoots.Add(fullRoot)) return;
                    foreach (var folder in Entries(fullRoot, true, warnings, cancellationToken))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var id = source == ModSource.Workshop ? Path.GetFileName(folder) : null;
                        var files = Entries(folder, false, warnings, cancellationToken)
                            .Where(p => Path.GetExtension(p).Equals(".mod", StringComparison.OrdinalIgnoreCase)).ToArray();
                        if (files.Length == 0) continue;
                        var metadata = ReadMetadata(folder, id, warnings, cancellationToken);
                        foreach (var file in files)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if (!visitedFiles.Add(file)) continue;
                            var name = Path.GetFileName(file);
                            metadata.TryGetValue(Path.GetFileNameWithoutExtension(file), out var info);
                            var identity = source == ModSource.Workshop ? $"workshop:{id}/{name}"
                                : "local:" + Path.GetRelativePath(fullRoot, file).Replace('\\', '/');
                            mods.Add(new InstalledMod(identity, name, file, source,
                                info?.Title ?? name, id ?? info?.WorkshopId, info?.Tags ?? Array.Empty<string>()));
                        }
                    }
                }
                ScanRoot(Path.Combine(gamePath, "mods"), ModSource.Local);
                foreach (var root in workshopPaths) ScanRoot(root, ModSource.Workshop);
                cancellationToken.ThrowIfCancellationRequested();
                return mods.AsReadOnly();
            }
            finally { Volatile.Write(ref diagnostics, warnings.AsReadOnly()); }
        }, cancellationToken);
    }

    private static string[] Entries(string folder, bool directories, List<string> warnings, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        try
        {
            var options = new EnumerationOptions { RecurseSubdirectories = false, IgnoreInaccessible = false, AttributesToSkip = FileAttributes.ReparsePoint };
            return (directories ? Directory.GetDirectories(folder, "*", options) : Directory.GetFiles(folder, "*", options))
                .Order(StringComparer.OrdinalIgnoreCase).ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { Warn(warnings, folder, ex.Message); return []; }
    }

    private sealed record Metadata(string? Title, string? WorkshopId, IReadOnlyList<string> Tags);
    private static Dictionary<string, Metadata> ReadMetadata(string folder, string? workshopId, List<string> warnings, CancellationToken token)
    {
        var result = new Dictionary<string, Metadata>(StringComparer.OrdinalIgnoreCase);
        var ambiguous = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Entries(folder, false, warnings, token).Where(p => Path.GetExtension(p).Equals(".info", StringComparison.OrdinalIgnoreCase)))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                if (file.Length > MetadataByteLimit) throw new InvalidDataException("Metadata exceeds the 1 MiB limit.");
                var bytes = new byte[checked((int)file.Length)];
                file.ReadExactly(bytes);
                using var input = new MemoryStream(bytes, false);
                using var reader = XmlReader.Create(input, new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
                    MaxCharactersInDocument = MetadataByteLimit, IgnoreComments = true
                });
                var xml = XDocument.Load(reader).Root ?? throw new InvalidDataException("Missing metadata root.");
                if (xml.Name.LocalName != "ModData") throw new InvalidDataException("Expected ModData metadata.");
                string? Value(string name) => xml.Elements().FirstOrDefault(e => e.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value.Trim();
                var mod = Value("mod");
                if (string.IsNullOrWhiteSpace(mod)) throw new InvalidDataException("Missing metadata mod identity.");
                if (mod.EndsWith(".mod", StringComparison.OrdinalIgnoreCase)) mod = mod[..^4];
                var id = Value("WorkshopID") ?? Value("id");
                if (workshopId != null && id != workshopId)
                    throw new InvalidDataException($"Metadata Workshop ID '{id}' does not match folder '{workshopId}'.");
                var title = Value("title");
                var tags = xml.Elements().FirstOrDefault(e => e.Name.LocalName == "tags")?.Elements()
                    .Select(e => e.Value.Trim()).Where(v => v.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [];
                var validId = !string.IsNullOrEmpty(id) && id.All(char.IsAsciiDigit) && id.Any(c => c != '0') ? id : null;
                if (ambiguous.Contains(mod)) continue;
                if (!result.TryAdd(mod, new Metadata(string.IsNullOrWhiteSpace(title) ? null : title, validId, tags)))
                {
                    result.Remove(mod);
                    ambiguous.Add(mod);
                    Warn(warnings, path, $"Duplicate metadata for '{mod}'; using filename fallback instead of an arbitrary title.");
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or XmlException)
            { Warn(warnings, path, ex.Message); }
        }
        return result;
    }
    private static void Warn(List<string> warnings, string path, string message)
    {
        var diagnostic = $"Cannot read inventory metadata/path '{path}': {message}";
        warnings.Add(diagnostic);
        Trace.TraceWarning(diagnostic);
    }
}

