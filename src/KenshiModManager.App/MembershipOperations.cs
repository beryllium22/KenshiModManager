using KenshiModManager.Core;
namespace KenshiModManager.App;
public sealed partial class WorkspaceViewModel {
 public void AddMods(IEnumerable<string> fileNames, bool enabled = true) {
 var profile = SelectedProfile ?? throw new InvalidOperationException("Select a profile before adding mods.");
 var requested = fileNames.ToArray();
 foreach (var name in requested) {
 try { LoadOrderCodec.ValidateFileName(name); }
 catch (FormatException error) { throw new FormatException($"Cannot add '{name}': {error.Message}", error); }
 }
 var known = profile.Mods.Select(m => m.FileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
 var lookup = Inventory.ToLookup(m => m.FileName, StringComparer.OrdinalIgnoreCase);
 var additions = new List<ProfileMod>();
 foreach (var name in requested) {
 if (!known.Add(name)) continue;
 var matches = lookup[name].ToArray();
 if (matches.Length == 0) throw new InvalidOperationException($"Cannot add '{name}': not installed. Install this exact filename and rescan before adding it.");
 if (matches.Length > 1) throw new InvalidOperationException($"Cannot add '{name}': multiple installations match. Resolve the duplicate local/Workshop installations and rescan before adding it.");
 additions.Add(new(name, enabled, matches[0].Id));
 }
 if (additions.Count == 0) return;
 BeginEdit(); profile.Mods.AddRange(additions); FinishEdit();
 }
 public void RemoveMods(IEnumerable<string> fileNames) {
 var profile = SelectedProfile ?? throw new InvalidOperationException("Select a profile before removing mods.");
 var names = fileNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
 if (!profile.Mods.Any(m => names.Contains(m.FileName))) return;
 BeginEdit(); profile.Mods.RemoveAll(m => names.Contains(m.FileName)); FinishEdit();
 }
}
