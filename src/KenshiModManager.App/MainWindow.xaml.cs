using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using KenshiModManager.Core;
namespace KenshiModManager.App;
public partial class MainWindow : Window {
 private readonly AppOptions options;
 private readonly StatePersistence persistence;
 private readonly DispatcherTimer saveTimer=new() {Interval=TimeSpan.FromMilliseconds(400)};
 private WorkspaceViewModel? vm;
 private GameWriteService? game;
 private LoadOrderSnapshot? snapshot;
 private bool busy,refreshing,dirty,closing,closeApproved,externalChange;
 private long revision;
 private string? lastVerifiedApply,lastEditorProfileId;
 private Point dragOrigin;
 private bool dragCandidate;

 public MainWindow(AppOptions options) {
  this.options=options; persistence=new(options.DataDirectory); InitializeComponent();
  SourceInitialized+=(_,_)=>ApplyNativeDarkCaption();
  saveTimer.Tick+=async (_,_)=> {saveTimer.Stop(); await SaveAsync();};
  Loaded+=async (_,_)=>await InitializeAsync(); Closing+=WindowClosing;
  Activated+=async (_,_)=> {if(!busy && game is not null && snapshot is not null) await CheckExternalAsync();};
 }
 private async Task InitializeAsync() {
  SetBusy(true,"Loading saved profiles and discovering Kenshi…");
  try {
   var state=await Task.Run(()=>persistence.Load());
   state.GamePath=options.GamePath ?? state.GamePath ?? await Task.Run(()=>SteamLibraryDiscovery.FindKenshiPaths().FirstOrDefault());
   if(state.GamePath is not null && state.WorkshopPaths.Count==0) state.WorkshopPaths=(await Task.Run(()=>SteamLibraryDiscovery.WorkshopPathsForGame(state.GamePath))).ToList();
   var initial=""; string? warning=null;
   if(!string.IsNullOrWhiteSpace(state.GamePath)) {
    game=new(state.GamePath,options.DataDirectory,options.ReadOnly);
    try { snapshot=await Task.Run(game.Read); initial=Encoding.UTF8.GetString(snapshot.Bytes).TrimStart('\uFEFF'); }
    catch(Exception error) {warning="Cannot read game configuration: "+error.Message+" Choose a valid game folder in Paths & settings.";}
   }
   vm=new(state,[],initial) {ReadOnly=options.ReadOnly}; DataContext=vm; vm.Changed+=WorkspaceChanged;

   await ScanInventoryAsync();
   if(warning is not null) Notice.Text=warning;
   else if(state.GamePath is null) Notice.Text="Kenshi was not discovered. Choose its folder in Paths & settings; you can still create/import profiles.";
   else if(options.ReadOnly) Notice.Text="Read-only game mode · Apply/Restore disabled. "+Notice.Text;
   WorkspaceChanged();
  } catch(Exception error) {
   System.Diagnostics.Trace.TraceError(error.ToString());
   try { Directory.CreateDirectory(options.DataDirectory); File.WriteAllText(Path.Combine(options.DataDirectory,"startup-error.txt"),error.ToString()); } catch(Exception loggingError) { System.Diagnostics.Trace.TraceError(loggingError.ToString()); }
   MessageBox.Show(this,error.Message+"\n\nState has not been reset. Preserve or move aside the damaged state file, restore a backup, or restart with --data-dir pointing to a new folder.\n\nState file: "+persistence.StatePath,"Cannot load manager",MessageBoxButton.OK,MessageBoxImage.Error);
   closeApproved=true; Close();
  } finally { SetBusy(false); }
 }
 private async Task ScanInventoryAsync() {
  if(vm is null) return;
  if(string.IsNullOrWhiteSpace(vm.State.GamePath)) {vm.ReplaceInventory([]); return;}
  try {
   var scanner=new ModInventoryScanner(); var inventory=await scanner.ScanAsync(vm.State.GamePath,vm.State.WorkshopPaths);
   vm.ReplaceInventory(inventory); Notice.Text=$"Scan complete · {inventory.Count} installed files. Profiles are not applied automatically.";
   if(scanner.Diagnostics.Count>0) {Notice.Text+=$" {scanner.Diagnostics.Count} metadata/path warnings (hover for details)."; Notice.ToolTip=string.Join(Environment.NewLine,scanner.Diagnostics);} else Notice.ToolTip=null;
  } catch(Exception error) {Notice.Text="Scan failed: "+error.Message+" Check paths and rescan. Previous inventory is retained.";}
 }
 private void SetBusy(bool value,string? message=null) {
  busy=value; if(Content is UIElement content) content.IsEnabled=!value;
  if(message is not null) Notice.Text=message;
  Mouse.OverrideCursor=value?Cursors.Wait:null; UpdateWriteButtons();
 }
 private void UpdateWriteButtons() {
  if(ReviewButton is null) return;
  GameMatchLabel.Text=GameOrderObservation.Describe(vm?.SelectedProfile,snapshot,externalChange);
  ReviewButton.IsEnabled=!busy && vm?.SelectedProfile is not null;
  RestoreButton.IsEnabled=!busy && !externalChange && !options.ReadOnly && snapshot is not null;
  ReloadButton.IsEnabled=!busy && game is not null;
  RestoreReason.Text=options.ReadOnly?"Restore disabled by --read-only":externalChange?"Reload the changed game snapshot before restoring":snapshot is null?"Choose a valid game folder and reload before restoring":"Restore is a game write, separate from profile Undo.";
  RestoreButton.ToolTip=RestoreReason.Text;
 }
 private string[] SelectedNames() => ModGrid.SelectedItems.OfType<ModRow>().Select(r=>r.FileName).ToArray();
 private void WorkspaceChanged() {
  dirty=true; revision++; SaveLabel.Text="Unsaved manager edits… · game untouched"; saveTimer.Stop(); saveTimer.Start(); RefreshView();
 }
 private async Task<bool> SaveAsync() {
  if(vm is null || !dirty) return true;
  var version=revision; var captured=StatePersistence.Snapshot(vm.State);
  try {
   await persistence.SaveAsync(captured);
   if(version==revision) {dirty=false; SaveLabel.ToolTip=null; SaveLabel.Text="Manager state saved · game unchanged by autosave";}
   return true;
  } catch(Exception error) {SaveLabel.Text="AUTOSAVE FAILED · edits remain in memory"; SaveLabel.ToolTip=error.Message; Notice.Text="Cannot save manager state: "+error.Message+" Check data-folder permissions/free space; closing offers retry."; return false;}
 }
 private async void WindowClosing(object? sender,CancelEventArgs e) {
  if(closeApproved) return; if(busy || closing) {e.Cancel=true; return;}
  if(!dirty) return; e.Cancel=true; closing=true; saveTimer.Stop();
  if(await SaveAsync()) {closeApproved=true; Close(); return;}
  var choice=MessageBox.Show(this,"Manager edits could not be saved. Retry after fixing the data folder, or choose Yes to close and lose unsaved manager edits. The game configuration is unaffected.","Unsaved edits",MessageBoxButton.YesNo,MessageBoxImage.Warning);
  closing=false; if(choice==MessageBoxResult.Yes) {closeApproved=true; Close();}
 }
 private void RefreshView(string[]? preserve=null) {
  if(vm is null || ModGrid is null) return; var selected=preserve ?? (lastEditorProfileId==vm.SelectedProfile?.Id?SelectedNames():[]); lastEditorProfileId=vm.SelectedProfile?.Id; refreshing=true;
  try {
   ModGrid.ItemsSource=vm.Filter(SearchBox.Text,FilterValue(SourceFilter),FilterValue(StatusFilter),FilterValue(EnabledFilter));
   var names=selected.ToHashSet(StringComparer.OrdinalIgnoreCase); foreach(var row in ModGrid.Items.OfType<ModRow>()) if(names.Contains(row.FileName)) ModGrid.SelectedItems.Add(row);
   ProfileList.SelectedItem=vm.SelectedProfile; ProfileHeading.Text=vm.SelectedProfile?.Name ?? "Create or import a profile";
   ProfileCountLabel.Text=$"{vm.Rows.Count} members · {vm.Rows.Count(r=>r.Enabled)} enabled";
   EmptyPanel.Visibility=ModGrid.Items.Count==0?Visibility.Visible:Visibility.Collapsed;
   EmptyTitle.Text=vm.Rows.Count==0?"Build your load order":"No matching mods";
   EmptyDescription.Text=vm.Rows.Count==0?"Add installed mods to this profile, or import an order into a new profile. Library rescans do not add members.":"Clear the filters to see every member of this profile.";
   var recorded=vm.State.Profiles.FirstOrDefault(p=>p.Id==vm.State.AppliedProfileId);
   AppliedLabel.Text=lastVerifiedApply is not null?"Last verified Apply: "+lastVerifiedApply:recorded is not null?$"Last recorded Apply: {recorded.Name} (saved history; content may since have changed)":"Last verified Apply: none recorded";

  } finally {refreshing=false;}
  UpdateDetails(); UpdateWriteButtons();
 }
 private static string FilterValue(ComboBox box)=>(box.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All";
 private bool Filtered => !string.IsNullOrWhiteSpace(SearchBox.Text) || FilterValue(SourceFilter)!="All" || FilterValue(StatusFilter)!="All" || FilterValue(EnabledFilter)!="All";
 private void UpdateDetails() {
  SelectionLabel.Text=$"{ModGrid.SelectedItems.Count} selected";
  SelectionBar.Visibility=ModGrid.SelectedItems.Count>0?Visibility.Visible:Visibility.Collapsed;
 }
 private void Run(Action action) {if(busy || vm is null) return; try {action(); RefreshView();} catch(Exception error) {ShowError(error);}}
 private void ShowError(Exception error) {Notice.Text=error.Message; MessageBox.Show(this,error.Message,"Action not completed",MessageBoxButton.OK,MessageBoxImage.Warning);}
 private async Task CheckExternalAsync() {
  if(game is null || snapshot is null) return;
  try {var current=await Task.Run(game.Read); if(current.Sha256!=snapshot.Sha256) {externalChange=true; Notice.Text="Kenshi's mods.cfg changed outside this manager. Explicitly Reload game snapshot before Apply/Restore. Your profile edits are retained.";} UpdateWriteButtons();}
  catch(Exception error) {externalChange=true; Notice.Text="Cannot check current game load order: "+error.Message; UpdateWriteButtons();}
 }
 private void FilterChanged(object sender,TextChangedEventArgs e) {if(!refreshing) RefreshView();}
 private void FilterSelectionChanged(object sender,SelectionChangedEventArgs e) {if(!refreshing) RefreshView();}
 private void ClearClick(object sender,RoutedEventArgs e) {SearchBox.Clear(); SourceFilter.SelectedIndex=EnabledFilter.SelectedIndex=StatusFilter.SelectedIndex=0; RefreshView();}
 private void ProfileSelectionChanged(object sender,SelectionChangedEventArgs e) {if(!refreshing && !busy && vm is not null && ProfileList.SelectedItem is ModProfile p && vm.SelectedProfile?.Id!=p.Id) Run(()=>vm.SelectProfile(p.Id));}
 private void ModSelectionChanged(object sender,SelectionChangedEventArgs e) {if(SelectionBar is not null) UpdateDetails();}
 private void UndoClick(object sender,RoutedEventArgs e)=>Run(()=>vm!.Undo());
 private void RedoClick(object sender,RoutedEventArgs e)=>Run(()=>vm!.Redo());

 private void DetailsClick(object sender,RoutedEventArgs e)=>Run(()=> {if(ModGrid.SelectedItem is ModRow row) StudioDialogs.Document(this,"Mod details","Details for one profile member. Profile notes and source are separate.",$"{row.Title}\n\nExact filename: {row.FileName}\n{row.Source} · {row.Status}\n{row.FullPath}\n{row.Tags}\n{row.WorkshopUrl}");});
 private void AddInstalledClick(object sender,RoutedEventArgs e)=>Run(()=> {if(vm?.SelectedProfile is not null) new InstalledModsDialog(this,vm).ShowDialog();});
 private void RemoveClick(object sender,RoutedEventArgs e)=>Run(()=> {var names=SelectedNames(); if(names.Length==0) return; vm!.RemoveMods(names); Notice.Text=$"Removed {names.Length} member(s) from this profile. Installed files and subscriptions are unchanged. Undo restores membership.";});
 private void IssuesClick(object sender,RoutedEventArgs e)=>Run(()=> {var issues=vm!.Rows.Where(r=>r.InstallationState!=ModInstallationState.Installed).Select(r=>$"{r.FileName}: {r.Status} ({(r.Enabled?"enabled — blocks Apply":"disabled — does not block Apply")})"); StudioDialogs.Document(this,"Profile issues","Installation checks, not compatibility advice. Disable or install missing entries, and resolve ambiguous installations before Apply.",string.Join("\n",issues.DefaultIfEmpty("Every member resolves to an installed filename. This is not a compatibility guarantee.")));});
}
