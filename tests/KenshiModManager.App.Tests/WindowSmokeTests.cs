using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using KenshiModManager.App;
using KenshiModManager.Core;
using Xunit;
namespace KenshiModManager.App.Tests;
public class WindowSmokeTests {
 [Fact] public void NativeWindowLoadsScansFiltersAndAutosavesWithoutGameWrites() {
  Exception? failure=null;
  var thread=new Thread(()=> {
   MainWindow? window=null;
   try {
    var root=ProjectFixture(); var game=Path.Combine(root,"game"); var data=Path.Combine(root,"manager"); Directory.CreateDirectory(Path.Combine(game,"data")); Directory.CreateDirectory(Path.Combine(game,"mods","test"));
    var workshop=Path.Combine(root,"workshop"); Directory.CreateDirectory(workshop); new ProfileStore(Path.Combine(data,"state.json")).Save(new ManagerState {WorkshopPaths=[workshop]});
    var cfg=Path.Combine(game,"data","mods.cfg"); File.WriteAllText(cfg,"123. Real.mod\r\nmissing.mod\r\n"); File.WriteAllText(Path.Combine(game,"mods","test","123. Real.mod"),""); var original=File.ReadAllBytes(cfg);
    if(Application.Current is null) {var app=new Application {Resources=new ResourceDictionary {Source=new Uri("pack://application:,,,/KenshiModManager.App;component/Theme.xaml")},ShutdownMode=ShutdownMode.OnExplicitShutdown};}
    window=new MainWindow(new(game,data,true)) {ShowInTaskbar=false}; window.Show();
    PumpUntil(()=>window.DataContext is WorkspaceViewModel && ((TextBlock)window.FindName("SaveLabel")).Text.StartsWith("Manager state saved"),TimeSpan.FromSeconds(15));
    var vm=Assert.IsType<WorkspaceViewModel>(window.DataContext);
    Assert.Equal(((SolidColorBrush)window.FindResource("Bg")).Color,((SolidColorBrush)window.Background).Color);
    Assert.Equal(14,window.FontSize);
    var applyButton=(Button)window.FindName("ReviewButton");
    var applyText=Descendants<TextBlock>(applyButton).Single(t=>t.Text==applyButton.Content?.ToString());
    Assert.Equal(((SolidColorBrush)applyButton.Foreground).Color,((SolidColorBrush)applyText.Foreground).Color);
    Assert.Single(vm.Inventory); Assert.Equal("123. Real.mod",vm.Rows[0].FileName);
    Assert.True(applyButton.IsEnabled); Assert.False(((Button)window.FindName("RestoreButton")).IsEnabled);
    Assert.Equal(Visibility.Collapsed,((WrapPanel)window.FindName("SelectionBar")).Visibility);
    ((TextBox)window.FindName("SearchBox")).Text="missing.mod"; Assert.Single(((DataGrid)window.FindName("ModGrid")).Items); Assert.Equal(2,vm.Rows.Count);
    InvokeEditor(window,"+  New profile",["Smoke profile"],"Create"); Assert.Equal("Smoke profile",vm.SelectedProfile!.Name); Assert.Empty(vm.SelectedProfile.Mods);
    InvokeAdd(window,"123. Real.mod","Add disabled"); Assert.False(Assert.Single(vm.SelectedProfile.Mods).Enabled); Assert.Equal("123. Real.mod",vm.SelectedProfile.Mods[0].FileName);
    InvokeEditor(window,"Rename",["Renamed profile"],"Save"); Assert.Equal("Renamed profile",vm.SelectedProfile!.Name);
    InvokeEditor(window,"Duplicate",["Smoke profile"],"Duplicate"); Assert.Equal(3,vm.Profiles.Count);
    var selectedBeforeImport=vm.SelectedProfile!.Id;
    Action assertPreviewIsNonMutating=()=> {Assert.Equal(3,vm.Profiles.Count); Assert.Equal(selectedBeforeImport,vm.SelectedProfile!.Id); Assert.Equal(original,File.ReadAllBytes(cfg));};
    InvokeImport(window,"Cancelled literal","123. Real.mod\nmissing.mod",false,assertPreviewIsNonMutating);
    assertPreviewIsNonMutating();
    InvokeImport(window,"Pasted literal","123. Real.mod\nmissing.mod",true,assertPreviewIsNonMutating);
    Assert.Equal(4,vm.Profiles.Count); Assert.Equal("Pasted literal",vm.SelectedProfile!.Name);
    Assert.Equal(new[]{"123. Real.mod","missing.mod"},vm.SelectedProfile.Mods.Select(m=>m.FileName));
    Assert.All(vm.SelectedProfile.Mods,m=>Assert.True(m.Enabled));
    vm.SelectProfile(vm.Profiles.First(p=>p.Name=="Smoke profile").Id); ((TextBox)window.FindName("SearchBox")).Clear();
    var grid=(DataGrid)window.FindName("ModGrid"); grid.SelectedItem=grid.Items.OfType<ModRow>().First(r=>r.FileName=="123. Real.mod"); Assert.Equal(Visibility.Visible,((WrapPanel)window.FindName("SelectionBar")).Visibility); Click(window,"Enable"); Assert.True(vm.SelectedProfile!.Mods.Single(m=>m.FileName=="123. Real.mod").Enabled);
    Click(window,"Disable"); Assert.False(vm.SelectedProfile.Mods[0].Enabled); Click(window,"Undo"); Assert.True(vm.SelectedProfile.Mods[0].Enabled); Click(window,"Redo"); Assert.False(vm.SelectedProfile.Mods[0].Enabled);
    PumpUntil(()=>((TextBlock)window.FindName("SaveLabel")).Text.StartsWith("Manager state saved"),TimeSpan.FromSeconds(10));
    var saved=new ProfileStore(Path.Combine(data,"state.json")).Load(); Assert.Equal("Smoke profile",saved.Profiles.First(p=>p.Id==saved.SelectedProfileId).Name); Assert.Null(saved.AppliedProfileId); Assert.Equal(original,File.ReadAllBytes(cfg));
   } catch(Exception error) {failure=error;} finally {window?.Close();}
  }) {IsBackground=true}; thread.SetApartmentState(ApartmentState.STA); thread.Start(); Assert.True(thread.Join(TimeSpan.FromSeconds(35)),"WPF smoke test timed out"); if(failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
 }
 private static IEnumerable<T> Descendants<T>(DependencyObject root) where T:DependencyObject {
  for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) {var child=VisualTreeHelper.GetChild(root,i); if(child is T value) yield return value; foreach(var nested in Descendants<T>(child)) yield return nested;}
 }
 private static void Click(Window window,string content) => Descendants<Button>(window).First(b=>b.Content?.ToString()==content).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
 private static void InvokeAdd(Window owner,string name,string accept) {
  Exception? error=null; var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(25)};
  timer.Tick+=(_,_)=> {var dialog=Application.Current.Windows.OfType<Window>().FirstOrDefault(w=>w.Owner==owner); if(dialog is null) return; timer.Stop();
   try {Descendants<TextBox>(dialog).First().Text="Real"; var grid=Descendants<DataGrid>(dialog).Single(); grid.SelectedItem=grid.Items.Cast<object>().First(); Click(dialog,accept);}
   catch(Exception problem) {error=problem; dialog.Close();}
  }; timer.Start(); Click(owner,"Add installed mods"); timer.Stop(); if(error is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
 }
 private static void InvokeImport(Window owner,string name,string text,bool confirm,Action onPreview) {
  Exception? error=null; var stage=0; var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(25)};
  timer.Tick+=(_,_)=> {var dialog=Application.Current.Windows.OfType<Window>().FirstOrDefault(w=>w.Owner==owner); if(dialog is null) return;
   try {
    if(stage==0) {var boxes=Descendants<TextBox>(dialog).ToArray(); Assert.Equal(2,boxes.Length); boxes[0].Text=name; boxes[1].Text=text; stage=1; Click(dialog,"Preview import");}
    else {timer.Stop(); Assert.Equal("Import preview — NEW profile",dialog.Title); var preview=Assert.Single(Descendants<TextBox>(dialog)); Assert.True(preview.IsReadOnly); Assert.Contains("123. Real.mod",preview.Text); Assert.Contains("missing.mod  [Missing]",preview.Text); onPreview(); stage=2; if(confirm) Click(dialog,"Create new profile"); else dialog.Close();}
   } catch(Exception problem) {error=problem; timer.Stop(); dialog.Close();}
  };
  timer.Start(); Click(owner,"Paste load order"); timer.Stop();
  if(error is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
  Assert.Equal(2,stage);
 }
 private static void InvokeEditor(Window owner,string action,string[] values,string accept) {
  Exception? error=null; var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(25)};
  timer.Tick+=(_,_)=> {var dialog=Application.Current.Windows.OfType<Window>().FirstOrDefault(w=>w.Owner==owner); if(dialog is null) return; timer.Stop();
   try {var boxes=Descendants<TextBox>(dialog).ToArray(); Assert.Equal(values.Length,boxes.Length); for(var i=0;i<values.Length;i++) boxes[i].Text=values[i]; Click(dialog,accept);}
   catch(Exception problem) {error=problem; dialog.Close();}
  }; timer.Start(); Click(owner,action); timer.Stop(); if(error is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
 }
 private static string ProjectFixture() {
  var directory=new DirectoryInfo(AppContext.BaseDirectory); while(directory is not null && !File.Exists(Path.Combine(directory.FullName,"Directory.Build.props"))) directory=directory.Parent;
  if(directory is null) throw new InvalidOperationException("Run tests in the repository so fixtures can use .hermes/test-data.");
  var path=Path.Combine(directory.FullName,".hermes","test-data","wpf-smoke-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path); return path;
 }
 private static void PumpUntil(Func<bool> ready,TimeSpan timeout) {
  var watch=System.Diagnostics.Stopwatch.StartNew(); while(!ready()) {if(watch.Elapsed>timeout) throw new TimeoutException("Window did not finish loading/saving"); var frame=new DispatcherFrame(); var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(20)}; timer.Tick+=(_,_)=> {timer.Stop(); frame.Continue=false;}; timer.Start(); Dispatcher.PushFrame(frame);}
 }
}
