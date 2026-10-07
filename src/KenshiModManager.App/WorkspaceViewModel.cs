using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using KenshiModManager.Core;
namespace KenshiModManager.App;
public abstract class Observable : INotifyPropertyChanged {
 public event PropertyChangedEventHandler? PropertyChanged;
 protected void Notify([CallerMemberName] string? property = null) => PropertyChanged?.Invoke(this, new(property));
}
public enum ModInstallationState { Installed, Missing, Ambiguous }
public sealed class ModRow {
 public required string FileName { get; init; }
 public required string Title { get; init; }
 public required string Status { get; init; }
 public ModInstallationState InstallationState { get; init; } = ModInstallationState.Missing;
 public required string Source { get; init; }
 public required int Position { get; init; }
 public required bool Enabled { get; init; }
 public InstalledMod? Installed { get; init; }
 public string FullPath => Installed?.FullPath ?? "Not installed — subscribe/install this exact filename.";
 public string Tags => string.Join(", ", Installed?.Tags ?? []);
 public string? WorkshopUrl => Installed?.WorkshopId is string id ? $"https://steamcommunity.com/sharedfiles/filedetails/?id={id}" : null;
}
public sealed partial class WorkspaceViewModel : Observable {
 public ManagerState State { get; }
 public IReadOnlyList<InstalledMod> Inventory { get; private set; }
 public ObservableCollection<ModProfile> Profiles { get; }
 public ObservableCollection<ModRow> Rows { get; } = [];
 public ModProfile? SelectedProfile { get; private set; }
 public bool ReadOnly { get; set; }
 public bool CanApply => !ReadOnly && SelectedProfile is not null && !Rows.Any(r => r.Enabled && r.InstallationState != ModInstallationState.Installed);
 public string Summary => $"{Rows.Count(r=>r.Enabled)} enabled  ·  {Inventory.Count} installed  ·  {Rows.Count(r=>r.Status=="Missing")} missing";
 public WorkspaceViewModel(ManagerState state, IReadOnlyList<InstalledMod> inventory, string? initialCfg = null) {
 State = state; Inventory = inventory;
 if(state.Profiles.Count == 0) state.Profiles.Add(ProfileStore.Create(state.Profiles, "Current Kenshi Setup", LoadOrderCodec.Parse(initialCfg ?? "")));
 Profiles = new(state.Profiles); SelectedProfile = Profiles.FirstOrDefault(p=>p.Id == state.SelectedProfileId) ?? Profiles[0];
 state.SelectedProfileId = SelectedProfile.Id; Refresh();
 }
 public void ReplaceInventory(IReadOnlyList<InstalledMod> inventory) { Inventory=inventory; Refresh(); Changed?.Invoke(); }
 private void Refresh() {
 Rows.Clear(); if(SelectedProfile is null) { Notify(nameof(SelectedProfile)); Notify(nameof(CanApply)); Notify(nameof(Summary)); return; }
 var lookup = Inventory.GroupBy(m=>m.FileName,StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>g.ToArray(),StringComparer.OrdinalIgnoreCase);
 for(int i=0;i<SelectedProfile.Mods.Count;i++) {
 var m=SelectedProfile.Mods[i]; lookup.TryGetValue(m.FileName,out var matches); var found=matches?.Length == 1 ? matches[0] : null;
 var installationState = matches is null ? ModInstallationState.Missing : matches.Length>1 ? ModInstallationState.Ambiguous : ModInstallationState.Installed;
 Rows.Add(new(){FileName=m.FileName,Title=found?.DisplayName ?? m.FileName,Status=installationState.ToString(),InstallationState=installationState,Source=found?.Source.ToString() ?? "—",Position=i+1,Enabled=m.Enabled,Installed=found});
 }
 Notify(nameof(SelectedProfile)); Notify(nameof(CanApply)); Notify(nameof(Summary));
 }
}
