namespace KenshiModManager.App;
public sealed partial class WorkspaceViewModel {
 public void Move(IEnumerable<string> names, int destination, bool filteredDrag=false) {
 if(filteredDrag) throw new InvalidOperationException("Drag is disabled while filtering. Use Move to position, Top or Bottom instead.");
 if(SelectedProfile is null) return;
 var set=names.ToHashSet(StringComparer.OrdinalIgnoreCase); var indices=SelectedProfile.Mods.Select((m,i)=>(m,i)).Where(x=>set.Contains(x.m.FileName)).Select(x=>x.i).ToArray();
 if(indices.Length==0) return; BeginEdit(); KenshiModManager.Core.ProfileEditor.Move(SelectedProfile,indices,destination); FinishEdit();
 }
 public IReadOnlyList<ModRow> Filter(string search, string source="All", string status="All", string enabled="All") => Rows.Where(r=>
 (string.IsNullOrWhiteSpace(search) || r.FileName.Contains(search,StringComparison.OrdinalIgnoreCase) || r.Title.Contains(search,StringComparison.OrdinalIgnoreCase) || r.Tags.Contains(search,StringComparison.OrdinalIgnoreCase)) &&
 (source=="All" || r.Source==source) && (status=="All" || r.Status==status) && (enabled=="All" || r.Enabled==(enabled=="Enabled"))).ToArray();
}
