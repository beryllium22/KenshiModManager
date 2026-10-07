using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using KenshiModManager.Core;
using Microsoft.Win32;
namespace KenshiModManager.App;
public partial class MainWindow {
 private static string? NameError(string[] values) => values[0].Any(char.IsControl)?"Profile names cannot contain control characters.":null;
 private void NewProfileClick(object sender,RoutedEventArgs e)=>Run(()=> {
  var copy=new CheckBox {Content=StudioDialogs.Text("Copy selected profile, including disabled members, notes and source"),IsChecked=false,IsEnabled=vm!.SelectedProfile is not null};
  var value=EditorDialog.Show(this,"New profile",[new("Profile name")],"Create",NameError,"Default: start with an empty profile. Rescanning the library does not populate it. Copy uses the selected editor profile, not the game's current file.",copy);
  if(value is not null) {if(copy.IsChecked==true) vm.Duplicate(value[0]); else vm.NewProfile(value[0]);}
 });
 private void RenameClick(object sender,RoutedEventArgs e)=>Run(()=> {if(vm!.SelectedProfile is null) return; var value=EditorDialog.Show(this,"Rename profile",[new("Profile name",vm.SelectedProfile.Name)],validate:NameError); if(value is not null) vm.Rename(value[0]);});
 private void DuplicateClick(object sender,RoutedEventArgs e)=>Run(()=> {if(vm!.SelectedProfile is null) return; var value=EditorDialog.Show(this,"Duplicate profile",[new("New profile name",vm.SelectedProfile.Name+" copy")],"Duplicate",NameError); if(value is not null) vm.Duplicate(value[0]);});
 private void DeleteClick(object sender,RoutedEventArgs e)=>Run(()=> {if(vm!.SelectedProfile is not null && MessageBox.Show(this,$"Delete '{vm.SelectedProfile.Name}'? Ctrl+Z can recover it. This will not change Kenshi's load order.","Delete profile",MessageBoxButton.YesNo,MessageBoxImage.Warning)==MessageBoxResult.Yes) vm.Delete();});
 private void PasteClick(object sender,RoutedEventArgs e)=>Run(()=> {
  var numbering=new CheckBox {Content="Remove author numbering (e.g. 1. Example.mod)",IsChecked=false};
  var values=EditorDialog.Show(this,"Paste into a new profile",[new("New profile name"),new("Exact .mod filenames in load order","",true)],"Preview import",v=> {var nameError=NameError(v); if(nameError is not null) return nameError; var parsed=LoadOrderCodec.Parse(v[1],numbering.IsChecked==true); return parsed.Count==0?"Paste at least one .mod filename.":null;},"Creates a NEW profile after preview. The selected profile is never appended to or replaced. Steam handles subscriptions; missing filenames remain visible. Filename prefixes are preserved unless you explicitly opt in below.",numbering);
  if(values is not null && PreviewImport(values[1],values[0],false,numbering.IsChecked==true)) vm!.Import(values[1],values[0],removeAuthorNumbering:numbering.IsChecked==true);
 });
 private async void ImportClick(object sender,RoutedEventArgs e) {
  if(vm is null || busy) return; var picker=new OpenFileDialog {Title="Import into a NEW profile",Filter="Profiles and load orders|*.json;*.cfg;*.txt|All files|*.*"}; if(picker.ShowDialog(this)!=true) return;
  SetBusy(true,"Reading import for preview…"); try {var text=await File.ReadAllTextAsync(picker.FileName,new UTF8Encoding(false,true)); var name=Path.GetFileNameWithoutExtension(picker.FileName); var json=Path.GetExtension(picker.FileName).Equals(".json",StringComparison.OrdinalIgnoreCase); if(PreviewImport(text,name,json)) {vm.Import(text,name,json); Notice.Text="Imported into a new profile. Selected profile was not appended to/replaced; game configuration untouched.";}} catch(Exception error) {ShowError(error);} finally {SetBusy(false);}
 }
 private async void ExportClick(object sender,RoutedEventArgs e) {
  if(vm?.SelectedProfile is null || busy) return; var picker=new SaveFileDialog {Title="Export selected profile",Filter="Portable profile JSON (includes disabled mods and notes)|*.json|Enabled load order CFG|*.cfg",FileName="profile",AddExtension=true}; if(picker.ShowDialog(this)!=true) return;
  SetBusy(true,"Exporting profile…"); try {
   var destination=Path.GetFullPath(picker.FileName); var protectedPaths=new[]{persistence.StatePath,vm.State.GamePath is null?null:Path.Combine(vm.State.GamePath,"data","mods.cfg")}.Where(x=>x is not null);
   if(protectedPaths.Any(x=>string.Equals(Path.GetFullPath(x!),destination,StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Export cannot overwrite manager state or the game's mods.cfg. Use Apply for game writes and choose another export file.");
   var text=vm.Export(picker.FilterIndex==1); await File.WriteAllTextAsync(destination,text,new UTF8Encoding(false)); if(await File.ReadAllTextAsync(destination)!=text) throw new IOException("Export read-back did not match; check destination permissions."); Notice.Text="Export verified: "+destination;
  } catch(Exception error) {ShowError(error);} finally {SetBusy(false);}
 }
 private void MetadataClick(object sender,RoutedEventArgs e)=>Run(()=> {if(vm!.SelectedProfile is null) return; var values=EditorDialog.Show(this,"Profile source & notes",[new("Collection/source URL",vm.SelectedProfile.SourceUrl ?? ""),new("Notes",vm.SelectedProfile.Notes ?? "",true)],validate:v=>string.IsNullOrWhiteSpace(v[0]) || IsWebUrl(v[0])?null:"Source URL must use https:// or http://."); if(values is not null) vm.SetMetadata(values[0],values[1]);});
 private static bool IsWebUrl(string value)=>Uri.TryCreate(value,UriKind.Absolute,out var uri) && uri.Scheme is "https" or "http";
 private void WorkshopClick(object sender,RoutedEventArgs e)=>Run(()=> {if(ModGrid.SelectedItem is ModRow {WorkshopUrl:not null} row) OpenUrl(row.WorkshopUrl);});
 private void ProfileSourceClick(object sender,RoutedEventArgs e)=>Run(()=> {if(vm!.SelectedProfile?.SourceUrl is string url) OpenUrl(url);});
 private static void OpenUrl(string url) {if(!IsWebUrl(url)) throw new InvalidOperationException("Only http/https links can be opened."); Process.Start(new ProcessStartInfo(url) {UseShellExecute=true});}
 private async void SettingsClick(object sender,RoutedEventArgs e) {
  if(vm is null || busy) return;
  var values=EditorDialog.Show(this,"Paths & settings",[new("Kenshi game folder",vm.State.GamePath ?? "",Folder:true),new("Workshop content folders (one per line)",string.Join(Environment.NewLine,vm.State.WorkshopPaths),true),new("Manager data folder (restart with --data-dir to change)",Path.GetFullPath(options.DataDirectory))],validate:v=> {
   if(!Directory.Exists(v[0]) || !File.Exists(Path.Combine(v[0],"data","mods.cfg"))) return "Select the Kenshi folder containing data/mods.cfg.";
   if(!string.Equals(Path.GetFullPath(v[2]),Path.GetFullPath(options.DataDirectory),StringComparison.OrdinalIgnoreCase)) return "The active data folder cannot be migrated in place. Cancel and restart with --data-dir <folder>.";
   var paths=v[1].Split(['\r','\n'],StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries); return paths.Any(x=>!Directory.Exists(x))?"A Workshop folder does not exist; remove it or select the correct folder.":null;
  },explanation:$"{(options.ReadOnly?"Read-only game mode is enabled.":"Game writes require explicit confirmation.")} State file: {persistence.StatePath}");
  if(values is null) return;
  SetBusy(true,"Updating paths and scanning installed mods…"); try {
   vm.State.GamePath=Path.GetFullPath(values[0]); vm.State.WorkshopPaths=values[1].Split(['\r','\n'],StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).ToList();
   if(vm.State.WorkshopPaths.Count==0) vm.State.WorkshopPaths=(await Task.Run(()=>SteamLibraryDiscovery.WorkshopPathsForGame(vm.State.GamePath))).ToList();
   game=new(vm.State.GamePath,options.DataDirectory,options.ReadOnly); snapshot=null; snapshot=await Task.Run(game.Read); externalChange=false; await ScanInventoryAsync(); WorkspaceChanged();
  } catch(Exception error) {WorkspaceChanged(); ShowError(error);} finally {SetBusy(false);}
 }
 private async void ScanClick(object sender,RoutedEventArgs e) {if(vm is null || busy) return; SetBusy(true,"Scanning local and Workshop folders…"); try {await ScanInventoryAsync();} finally {SetBusy(false);}}
 private async void ReloadClick(object sender,RoutedEventArgs e) {
  if(game is null || busy) return;
  if(MessageBox.Show(this,"Reload the current game snapshot for the next Apply/Restore? This acknowledges external changes but does not import them or modify the selected profile. To import them as a new profile, use Import file.","Reload game snapshot",MessageBoxButton.OKCancel,MessageBoxImage.Information)!=MessageBoxResult.OK) return;
  SetBusy(true,"Reading current game configuration…"); try {snapshot=await Task.Run(game.Read); externalChange=false; Notice.Text="Game snapshot reloaded. Profile edits retained; no game write performed.";} catch(Exception error) {snapshot=null; externalChange=true; ShowError(error);} finally {SetBusy(false);}
 }
 private async void ApplyClick(object sender,RoutedEventArgs e) {
  if(vm?.SelectedProfile is null || busy) return;
  var profile=StatePersistence.Snapshot(vm.State).Profiles.First(p=>p.Id==vm.SelectedProfile.Id);
  if(game is not null && snapshot is not null) {SetBusy(true,"Checking the observed game order before review…"); try {await CheckExternalAsync();} finally {SetBusy(false);}}
  var enabled=profile.Mods.Where(m=>m.Enabled).Select(m=>m.FileName).ToArray(); var inventory=vm.Inventory.ToArray();
  var blockers=new List<string>(); if(options.ReadOnly) blockers.Add("Read-only mode: final Apply is disabled; profile editing and review remain available.");
  if(game is null || snapshot is null) blockers.Add("Choose a valid game folder and reload its snapshot before Apply.");
  if(externalChange) blockers.Add("Game snapshot is stale or unreadable. Reload it explicitly; your profile edits are retained.");
  if(enabled.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=enabled.Length) blockers.Add("Duplicate enabled filenames must be removed before Apply.");
  var byName=inventory.ToLookup(m=>m.FileName,StringComparer.OrdinalIgnoreCase);
  foreach(var name in enabled) {
   try {LoadOrderCodec.ValidateFileName(name);} catch(FormatException error) {blockers.Add($"{name}: {error.Message}");}
   var matches=byName[name].ToArray(); if(matches.Length!=1) blockers.Add($"{name}: {(matches.Length==0?"Missing":"Ambiguous")} — install/disable or resolve duplicate installations.");
   else if(!File.Exists(matches[0].FullPath)) blockers.Add($"{name}: installed file is no longer readable/present. Rescan the library.");
  }
  IReadOnlyList<string>? old=null;
  if(snapshot is not null && !externalChange) {try {old=LoadOrderCodec.Parse(new UTF8Encoding(false,true).GetString(snapshot.Bytes)); if(old.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=old.Count) {old=null; blockers.Add("Current mods.cfg contains duplicate filenames. Preserve/correct it, then reload.");}} catch(Exception error) when(error is FormatException or DecoderFallbackException) {blockers.Add("Current mods.cfg is not a valid UTF-8 literal load order. Preserve/correct it, then reload.");}}
  var diff=old is null?"Current game order unknown":$"{enabled.Except(old,StringComparer.OrdinalIgnoreCase).Count()} added · {old.Except(enabled,StringComparer.OrdinalIgnoreCase).Count()} removed · order {(old.SequenceEqual(enabled,StringComparer.OrdinalIgnoreCase)?"unchanged":"changed")}";
  var preview=$"PROPOSED ENABLED ORDER ({enabled.Length})\n{string.Join("\n",enabled)}\n\nCURRENT OBSERVED ORDER\n{(old is null?"Unknown — no reliable comparison":string.Join("\n",old))}\n\nBLOCKERS\n{(blockers.Count==0?"None found in filename/installation checks. Compatibility is not inferred.":string.Join("\n",blockers))}";
  var summary=$"Review “{profile.Name}” · {diff}\nTarget: {snapshot?.Path ?? "No game folder configured"}\n{GameOrderObservation.Describe(profile,snapshot,externalChange)}\nExact current bytes will be backed up under {Path.Combine(options.DataDirectory,"backups")}. This does not download mods.";
  var expected=snapshot; var service=game;
  if(!StudioDialogs.Review(this,"Review and use profile",summary,preview,"Apply to Kenshi",blockers.Count==0?null:$"Apply blocked ({blockers.Count} reason(s)). {blockers[0]} See the scrollable review for all blockers.") || blockers.Count!=0 || expected is null || service is null) return;
  SetBusy(true,"Applying with backup and read-back verification…"); try {
   var result=await Task.Run(()=>service.Apply(profile,inventory,expected)); var verified=await Task.Run(service.Read); if(verified.Sha256!=result.Sha256) throw new IOException("Game read-back changed after Apply. Check the backup and reload before continuing.");
   snapshot=verified; externalChange=false; lastVerifiedApply=$"{profile.Name} · {result.EnabledCount} enabled (verified game write; later edits stay separate)"; vm.State.AppliedProfileId=profile.Id; WorkspaceChanged(); await SaveAsync(); Notice.Text=$"Apply verified · {result.EnabledCount} enabled. Exact backup: {result.BackupPath}";
  } catch(Exception error) {ShowError(error); await CheckExternalAsync();} finally {SetBusy(false);}
 }
 private async void RestoreClick(object sender,RoutedEventArgs e) {
  if(vm is null || game is null || snapshot is null || busy || options.ReadOnly) return;
  var backups=Path.Combine(Path.GetFullPath(options.DataDirectory),"backups"); var picker=new OpenFileDialog {Title="Choose an exact manager backup to restore",Filter="Manager load-order backups|mods-*.cfg|CFG files|*.cfg",InitialDirectory=Directory.Exists(backups)?backups:Path.GetFullPath(options.DataDirectory)};
  if(picker.ShowDialog(this)!=true) return;
  var expected=snapshot; var service=game; byte[] reviewed; string preview;
  SetBusy(true,"Reading backup for review…");
  try {
   if(string.Equals(Path.GetFullPath(picker.FileName),Path.GetFullPath(expected.Path),StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Choose a saved backup, not the current game configuration.");
   reviewed=await File.ReadAllBytesAsync(picker.FileName); var names=LoadOrderCodec.Parse(new UTF8Encoding(false,true).GetString(reviewed));
   if(names.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=names.Count) throw new InvalidDataException("Backup contains duplicate filenames. Restore is blocked.");
   preview=LoadOrderCodec.Format(names); await CheckExternalAsync();
  } catch(Exception error) {ShowError(error); return;} finally {SetBusy(false);}
  if(!StudioDialogs.Review(this,"Review exact backup restore",$"Restore exact bytes from:\n{picker.FileName}\nTo: {expected.Path}\nCurrent game bytes will first be backed up. This leaves selected profile edits and the last verified Apply history separate.",preview,"Restore backup",externalChange?"Snapshot is stale/unreadable. Reload before restoring.":null) || externalChange) return;
  SetBusy(true,"Restoring exact backup with read-back verification…"); try {
   // Lock the reviewed source against writes/deletion through commit; never restore a changed preview.
   using var backupGuard=new FileStream(picker.FileName,FileMode.Open,FileAccess.Read,FileShare.Read);
   var reread=await File.ReadAllBytesAsync(picker.FileName);
   if(!reviewed.AsSpan().SequenceEqual(reread)) throw new IOException("Backup changed since review. Review the current backup before restoring.");
   var result=await Task.Run(()=>service.Restore(picker.FileName,expected)); var verified=await Task.Run(service.Read); if(verified.Sha256!=result.Sha256) throw new IOException("Read-back changed after Restore. Reload before continuing."); snapshot=verified; externalChange=false; vm.State.AppliedProfileId=null; WorkspaceChanged(); await SaveAsync(); Notice.Text="Restore verified. Previous target backed up at "+result.BackupPath;
  } catch(Exception error) {ShowError(error); await CheckExternalAsync();} finally {SetBusy(false);}
 }
}
