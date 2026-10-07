using System.Text.RegularExpressions;
namespace KenshiModManager.Core;
public static class LoadOrderCodec
{
    public static IReadOnlyList<string> Parse(string text, bool removeAuthorNumbering = false)
    {
        ArgumentNullException.ThrowIfNull(text);
        var result = new List<string>();
        foreach (var line in text.TrimStart('\uFEFF').Split(['\r', '\n']))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var name = removeAuthorNumbering ? Regex.Replace(line, @"^\s*\d+[.)]\s+", "") : line;
            ValidateFileName(name);
            result.Add(name);
        }
        return result;
    }
    public static string Format(IEnumerable<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);
        var list = names.ToArray();
        foreach (var name in list) ValidateFileName(name);
        return list.Length == 0 ? "" : string.Join("\r\n", list) + "\r\n";
    }
    public static void ValidateFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || !name.EndsWith(".mod", StringComparison.OrdinalIgnoreCase)
            || name.Any(c => char.IsControl(c) || "<>:\"/\\|?*".Contains(c))
            || name.Length > 255 || name[..^4].Length == 0
            || Regex.IsMatch(name, @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])\.", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            throw new FormatException("Use an exact filename ending in .mod; names are not automatically repaired.");
    }
}
