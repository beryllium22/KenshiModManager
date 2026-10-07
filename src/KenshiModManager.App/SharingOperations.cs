using KenshiModManager.Core;
namespace KenshiModManager.App;
public sealed partial class WorkspaceViewModel {
 private void AddAndSelect(ModProfile profile) { State.Profiles.Add(profile); Profiles.Add(profile); SelectedProfile=profile; State.SelectedProfileId=profile.Id; FinishEdit(); }
 public void Rename(string? name) {
 if(SelectedProfile is null) return; var valid=ProfileStore.Create(State.Profiles.Where(p=>p.Id!=SelectedProfile.Id),name,[]).Name;
 BeginEdit(); SelectedProfile.Name=valid; var index=Profiles.IndexOf(SelectedProfile); Profiles[index]=SelectedProfile; FinishEdit();
 }
 public ModProfile Duplicate(string? name=null) { var p=ProfileStore.Duplicate(State.Profiles,SelectedProfile ?? throw new InvalidOperationException("Select a profile."),name); BeginEdit(); AddAndSelect(p); return p; }
 public void Delete() {
 if(SelectedProfile is null) return; BeginEdit(); State.Profiles.Remove(SelectedProfile); Profiles.Remove(SelectedProfile);
 if(State.AppliedProfileId==SelectedProfile.Id) State.AppliedProfileId=null;
 SelectedProfile=Profiles.FirstOrDefault(); State.SelectedProfileId=SelectedProfile?.Id; FinishEdit();
 }
 public ModProfile Import(string text,string? name=null,bool json=false,bool removeAuthorNumbering=false) {
 var p=json ? ProfileStore.ImportProfile(text,State.Profiles) : ProfileStore.Create(State.Profiles,name,LoadOrderCodec.Parse(text,removeAuthorNumbering)); BeginEdit(); AddAndSelect(p); return p;
 }
 public string Export(bool json=false) {
 var p=SelectedProfile ?? throw new InvalidOperationException("Select a profile to export.");
 if(!json) return LoadOrderCodec.Format(p.Mods.Where(m=>m.Enabled).Select(m=>m.FileName));
 // Sharing references describe the exported literal member, not the scanner's filename casing.
 // Project a copy so neither member spelling nor stored scanner identity is modified.
 var portable=new ModProfile {Id=p.Id,Name=p.Name,SourceUrl=p.SourceUrl,Notes=p.Notes,
  Mods=p.Mods.Select(PortableMember).ToList()};
 return ProfileStore.ExportProfile(portable);
 }
 private static ProfileMod PortableMember(ProfileMod member) {
 var reference=member.InstalledId;
 if(reference?.StartsWith("workshop:",StringComparison.Ordinal)!=true) return member;
 var slash=reference.IndexOf('/',9);
 // Leave malformed/different-file references intact for Core's strict validation to reject.
 if(slash<10 || !string.Equals(reference[(slash+1)..],member.FileName,StringComparison.OrdinalIgnoreCase)) return member;
 return member with {InstalledId=reference[..(slash+1)]+member.FileName};
 }
 public void SetMetadata(string? sourceUrl,string? notes) {
 if(SelectedProfile is null) return; BeginEdit(); SelectedProfile.SourceUrl=sourceUrl; SelectedProfile.Notes=notes; FinishEdit();
 }
}
