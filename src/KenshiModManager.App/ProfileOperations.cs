using KenshiModManager.Core;
namespace KenshiModManager.App;
public sealed partial class WorkspaceViewModel {
 private readonly Stack<string> undo = new();
 private readonly Stack<string> redo = new();
 public event Action? Changed;
 public bool CanUndo => undo.Count > 0;
 public bool CanRedo => redo.Count > 0;
 private string Capture() => System.Text.Json.JsonSerializer.Serialize(State);
 private void BeginEdit() { undo.Push(Capture()); redo.Clear(); }
 private void FinishEdit() { Refresh(); Notify(nameof(CanUndo)); Notify(nameof(CanRedo)); Changed?.Invoke(); }
 private void Recover(string json) {
 var previous=System.Text.Json.JsonSerializer.Deserialize<ManagerState>(json)!;
 State.Profiles=previous.Profiles; State.SelectedProfileId=previous.SelectedProfileId;
 if(State.AppliedProfileId is not null && !State.Profiles.Any(p=>p.Id==State.AppliedProfileId)) State.AppliedProfileId=null;
 Profiles.Clear(); foreach(var p in State.Profiles) Profiles.Add(p);
 SelectedProfile=Profiles.FirstOrDefault(p=>p.Id==State.SelectedProfileId); FinishEdit();
 }
 public ModProfile NewProfile(string? name) {
 BeginEdit(); var p=ProfileStore.Create(State.Profiles,name,[]); State.Profiles.Add(p); Profiles.Add(p); SelectedProfile=p; State.SelectedProfileId=p.Id; FinishEdit(); return p;
 }
 public void SelectProfile(string id) {
 SelectedProfile=Profiles.First(p=>p.Id==id); State.SelectedProfileId=id; Refresh(); Changed?.Invoke();
 }
 public void SetEnabled(IEnumerable<string> names, bool enabled) {
 if(SelectedProfile is null) return; var set=names.ToHashSet(StringComparer.OrdinalIgnoreCase); BeginEdit();
 for(int i=0;i<SelectedProfile.Mods.Count;i++) if(set.Contains(SelectedProfile.Mods[i].FileName)) SelectedProfile.Mods[i]=SelectedProfile.Mods[i] with {Enabled=enabled}; FinishEdit();
 }
 public void Undo() { if(!CanUndo) return; redo.Push(Capture()); Recover(undo.Pop()); }
 public void Redo() { if(!CanRedo) return; undo.Push(Capture()); Recover(redo.Pop()); }
}
