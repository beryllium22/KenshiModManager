using System.IO;
namespace KenshiModManager.App;
public sealed record AppOptions(string? GamePath, string DataDirectory, bool ReadOnly) {
 public static AppOptions Parse(string[] args) {
 string? game = null; var data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KenshiModManager"); bool readOnly = false;
 for (int i = 0; i < args.Length; i++) {
 var parts = args[i].Split('=', 2); var key = parts[0];
 if (key == "--read-only") { readOnly = true; continue; }
 if (key is not ("--game-path" or "--data-dir")) throw new ArgumentException($"Unknown option: {key}");
 var value = parts.Length == 2 ? parts[1] : ++i < args.Length ? args[i] : throw new ArgumentException($"{key} requires a path.");
 if (string.IsNullOrWhiteSpace(value) || value.StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException($"{key} requires a path.");
 if (key == "--game-path") game = value; else data = value;
 }
 return new(game, data, readOnly);
 }
}
