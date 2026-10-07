using System.Diagnostics;
using System.Security;
using System.Text;
using Microsoft.Win32;
namespace KenshiModManager.Core;

public static class SteamLibraryDiscovery
{
    public static IReadOnlyList<string> FindKenshiPaths() => FindKenshiPaths(DefaultRoots());
    /// <summary>Discover within explicit Steam roots, without registry or drive fallback probes.</summary>
    public static IReadOnlyList<string> FindKenshiPaths(IEnumerable<string> steamRoots) =>
        Libraries(steamRoots).Select(root => Path.Combine(root, "steamapps", "common", "Kenshi"))
            .Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public static IReadOnlyList<string> WorkshopPathsForGame(string gamePath) => WorkshopPathsForGame(gamePath, DefaultRoots());
    public static IReadOnlyList<string> WorkshopPathsForGame(string gamePath, IEnumerable<string> steamRoots)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gamePath);
        var libraries = Libraries(steamRoots).ToList();
        var ancestor = new DirectoryInfo(Path.GetFullPath(gamePath));
        while (ancestor != null)
        {
            if (ancestor.Name.Equals("steamapps", StringComparison.OrdinalIgnoreCase))
            {
                if (ancestor.Parent != null) libraries.Insert(0, ancestor.Parent.FullName);
                break;
            }
            ancestor = ancestor.Parent;
        }
        return libraries.Select(root => Path.Combine(root, "steamapps", "workshop", "content", "233860"))
            .Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IReadOnlyList<string> Libraries(IEnumerable<string> steamRoots)
    {
        ArgumentNullException.ThrowIfNull(steamRoots);
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var expanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string path)
        {
            try { roots.Add(Path.TrimEndingDirectorySeparator(Path.GetFullPath(path))); }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException)
            { Trace.TraceWarning($"Invalid Steam library path '{path}': {ex.Message}"); }
        }
        foreach (var candidate in steamRoots)
        {
            Add(candidate);
            string root;
            try { root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate)); }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException)
            { Trace.TraceWarning($"Cannot inspect Steam root '{candidate}': {ex.Message}"); continue; }
            if (!expanded.Add(root)) continue;
            foreach (var vdf in new[] { Path.Combine(root, "steamapps", "libraryfolders.vdf"), Path.Combine(root, "config", "libraryfolders.vdf") })
            {
                if (!File.Exists(vdf)) continue;
                try
                {
                    using var file = new FileStream(vdf, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    if (file.Length > 4 * 1024 * 1024) throw new InvalidDataException("Library metadata exceeds the 4 MiB limit.");
                    var bytes = new byte[checked((int)file.Length)];
                    file.ReadExactly(bytes);
                    var nodes = ParseVdf(Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF'));
                    var entries = nodes.FirstOrDefault(n => n.Key.Equals("libraryfolders", StringComparison.OrdinalIgnoreCase))?.Children;
                    if (entries == null) throw new InvalidDataException("Missing libraryfolders object.");
                    foreach (var entry in entries.Where(n => n.Key.Length > 0 && n.Key.All(char.IsAsciiDigit)))
                    {
                        var path = entry.Value ?? entry.Children?.FirstOrDefault(n => n.Key.Equals("path", StringComparison.OrdinalIgnoreCase))?.Value;
                        if (!string.IsNullOrWhiteSpace(path)) Add(path);
                    }
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or FormatException)
                { Trace.TraceWarning($"Cannot read Steam library metadata '{vdf}': {ex.Message}"); }
            }
        }
        return roots.Order(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IEnumerable<string> DefaultRoots()
    {
        var roots = new List<string>();
        if (OperatingSystem.IsWindows())
        {
            foreach (var spec in new[] {
                (Hive: RegistryHive.CurrentUser, View: RegistryView.Default, Key: @"Software\Valve\Steam", Value: "SteamPath"),
                (Hive: RegistryHive.LocalMachine, View: RegistryView.Registry32, Key: @"Software\Valve\Steam", Value: "InstallPath"),
                (Hive: RegistryHive.LocalMachine, View: RegistryView.Registry64, Key: @"Software\Valve\Steam", Value: "InstallPath") })
            {
                try
                {
                    using var hive = RegistryKey.OpenBaseKey(spec.Hive, spec.View);
                    using var key = hive.OpenSubKey(spec.Key);
                    if (key?.GetValue(spec.Value) is string path && !string.IsNullOrWhiteSpace(path)) roots.Add(path);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
                { Trace.TraceWarning($"Cannot read Steam registry location: {ex.Message}"); }
            }
        }
        foreach (var basePath in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) })
            if (!string.IsNullOrEmpty(basePath)) roots.Add(Path.Combine(basePath, "Steam"));
        // Fixed known locations only: never recursively enumerate drives or games.
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed))
            foreach (var folder in new[] { "SteamLibrary", "Steam", "Program Files (x86)/Steam", "Program Files/Steam" })
                roots.Add(Path.Combine(drive.Name, folder));
        return roots.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private sealed record Node(string Key, string? Value, List<Node>? Children);
    private sealed record Token(string Text, bool Brace = false);
    private static List<Node> ParseVdf(string text)
    {
        var tokens = new List<Token>();
        for (var i = 0; i < text.Length;)
        {
            if (char.IsWhiteSpace(text[i])) { i++; continue; }
            if (text[i] == '/' && i + 1 < text.Length && text[i + 1] == '/')
            { while (i < text.Length && text[i] != '\n') i++; continue; }
            if (text[i] is '{' or '}') { tokens.Add(new Token(text[i++].ToString(), true)); continue; }
            if (text[i] == '"')
            {
                var value = new StringBuilder();
                i++;
                while (i < text.Length && text[i] != '"')
                {
                    if (text[i] == '\\' && i + 1 < text.Length && text[i + 1] is '\\' or '"') i++;
                    value.Append(text[i++]);
                }
                if (i == text.Length) throw new FormatException("Unterminated quoted VDF value.");
                i++;
                tokens.Add(new Token(value.ToString()));
            }
            else
            {
                var start = i;
                while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] is not '{' and not '}') i++;
                tokens.Add(new Token(text[start..i]));
            }
        }
        var position = 0;
        List<Node> Object(int depth, bool nested)
        {
            if (depth > 64) throw new FormatException("VDF nesting exceeds 64 levels.");
            var nodes = new List<Node>();
            while (position < tokens.Count)
            {
                var key = tokens[position++];
                if (key.Brace)
                {
                    if (key.Text == "}" && nested) return nodes;
                    throw new FormatException("Unexpected VDF brace.");
                }
                if (position == tokens.Count) throw new FormatException("Missing VDF value.");
                var value = tokens[position++];
                if (value.Brace)
                {
                    if (value.Text != "{") throw new FormatException("Missing VDF object value.");
                    nodes.Add(new Node(key.Text, null, Object(depth + 1, true)));
                }
                else nodes.Add(new Node(key.Text, value.Text, null));
            }
            if (nested) throw new FormatException("Unclosed VDF object.");
            return nodes;
        }
        return Object(0, false);
    }
}
