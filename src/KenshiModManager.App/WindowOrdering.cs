using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
namespace KenshiModManager.App;
public partial class MainWindow {
 private sealed record ModDrag(MainWindow Owner,string ProfileId,string[] Names);
 private void ToggleClick(object sender,RoutedEventArgs e)=>Run(()=> {if(sender is CheckBox {DataContext:ModRow row} box) {var selected=SelectedNames(); vm!.SetEnabled([row.FileName],box.IsChecked==true); RefreshView(selected);} e.Handled=true;});
 private void EnableClick(object sender,RoutedEventArgs e)=>Run(()=>vm!.SetEnabled(SelectedNames(),true));
 private void DisableClick(object sender,RoutedEventArgs e)=>Run(()=>vm!.SetEnabled(SelectedNames(),false));
 private void MoveSelected(int destination) {var names=SelectedNames(); if(names.Length==0) return; vm!.Move(names,destination); RefreshView(names); if(ModGrid.SelectedItem is not null) ModGrid.ScrollIntoView(ModGrid.SelectedItem);}
 private void StepSelection(bool down) {
  var names=SelectedNames().ToHashSet(StringComparer.OrdinalIgnoreCase); if(names.Count==0 || vm?.SelectedProfile is null) return;
  var indices=vm.SelectedProfile.Mods.Select((m,i)=>(m,i)).Where(x=>names.Contains(x.m.FileName)).Select(x=>x.i).ToArray();
  var destination=down?Math.Min(vm.SelectedProfile.Mods.Count,indices.Max()+2):Math.Max(0,indices.Min()-1); MoveSelected(destination);
 }
 private void UpClick(object sender,RoutedEventArgs e)=>Run(()=>StepSelection(false));
 private void DownClick(object sender,RoutedEventArgs e)=>Run(()=>StepSelection(true));
 private void TopClick(object sender,RoutedEventArgs e)=>Run(()=>MoveSelected(0));
 private void BottomClick(object sender,RoutedEventArgs e)=>Run(()=>MoveSelected(vm!.SelectedProfile?.Mods.Count ?? 0));
 private void PositionClick(object sender,RoutedEventArgs e)=>Run(()=> {
  if(vm!.SelectedProfile is null || SelectedNames().Length==0) return;
  var selected=SelectedNames().ToHashSet(StringComparer.OrdinalIgnoreCase); var remaining=vm.SelectedProfile.Mods.Select((m,i)=>(m,i)).Where(x=>!selected.Contains(x.m.FileName)).ToArray();
  var maximum=remaining.Length+1;
  var values=EditorDialog.Show(this,"Move selection",[new($"New first position (1–{maximum})","1")],"Move",v=>int.TryParse(v[0],out var position) && position>=1 && position<=maximum?null:$"Enter a whole-number position from 1 to {maximum}. Relative selection order is preserved.","Positions refer to the full unfiltered order, not the search result.");
  if(values is null) return; var target=int.Parse(values[0])-1; MoveSelected(target==remaining.Length?vm.SelectedProfile.Mods.Count:remaining[target].i);
 });
 private void WindowKeyDown(object sender,KeyEventArgs e) {
  if(busy || vm is null) return; var control=(Keyboard.Modifiers & ModifierKeys.Control)!=0; var alt=(Keyboard.Modifiers & ModifierKeys.Alt)!=0; var key=e.Key==Key.System?e.SystemKey:e.Key;
  if(control && key==Key.F) {SearchBox.Focus(); SearchBox.SelectAll(); e.Handled=true; return;}
  if(e.OriginalSource is TextBox || Ancestor<TextBox>(e.OriginalSource as DependencyObject) is not null) return;
  if(control && key==Key.Z) {Run(()=>vm.Undo()); e.Handled=true;}
  else if(control && key==Key.Y) {Run(()=>vm.Redo()); e.Handled=true;}
  else if(ModGrid.IsKeyboardFocusWithin && alt && key is Key.Up or Key.Down) {Run(()=>StepSelection(key==Key.Down)); e.Handled=true;}
  else if(ModGrid.IsKeyboardFocusWithin && key==Key.Space) {Run(()=> {var selected=ModGrid.SelectedItems.OfType<ModRow>().ToArray(); if(selected.Length>0) vm.SetEnabled(selected.Select(r=>r.FileName),!selected.All(r=>r.Enabled));}); e.Handled=true;}
 }
 private static T? Ancestor<T>(DependencyObject? item) where T:DependencyObject {
  while(item is not null) {if(item is T match) return match; item=item is Visual || item is System.Windows.Media.Media3D.Visual3D?VisualTreeHelper.GetParent(item):LogicalTreeHelper.GetParent(item);} return null;
 }
 private void DragStart(object sender,MouseButtonEventArgs e) {
  dragOrigin=e.GetPosition(ModGrid); var row=Ancestor<DataGridRow>(e.OriginalSource as DependencyObject);
  dragCandidate=!busy && row?.Item is ModRow && Ancestor<CheckBox>(e.OriginalSource as DependencyObject) is null;
  if(dragCandidate && row!.IsSelected && Keyboard.Modifiers==ModifierKeys.None) {ModGrid.Focus(); e.Handled=true;}
 }
 private void DragMove(object sender,MouseEventArgs e) {
  if(!dragCandidate || e.LeftButton!=MouseButtonState.Pressed || vm?.SelectedProfile is null) return;
  var point=e.GetPosition(ModGrid); if(Math.Abs(point.X-dragOrigin.X)<SystemParameters.MinimumHorizontalDragDistance && Math.Abs(point.Y-dragOrigin.Y)<SystemParameters.MinimumVerticalDragDistance) return;
  dragCandidate=false;
  if(Filtered) {Notice.Text="Drag is disabled while filtering. Use Top, Bottom or Move to…; these map to the full order."; return;}
  var names=SelectedNames(); if(names.Length==0) return;
  DragDrop.DoDragDrop(ModGrid,new ModDrag(this,vm.SelectedProfile.Id,names),DragDropEffects.Move);
 }
 private void GridDragOver(object sender,DragEventArgs e) {e.Effects=!busy && !Filtered && e.Data.GetData(typeof(ModDrag)) is ModDrag d && d.Owner==this && d.ProfileId==vm?.SelectedProfile?.Id?DragDropEffects.Move:DragDropEffects.None; e.Handled=true;}
 private void GridDrop(object sender,DragEventArgs e) {
  e.Handled=true; if(busy || vm?.SelectedProfile is null || e.Data.GetData(typeof(ModDrag)) is not ModDrag d || d.Owner!=this || d.ProfileId!=vm.SelectedProfile.Id) return;
  Run(()=> {if(Filtered) throw new InvalidOperationException("Clear filters before dragging. Use Move to… for filtered editing."); var target=Ancestor<DataGridRow>(e.OriginalSource as DependencyObject); var destination=target?.Item is ModRow row?row.Position-1+(e.GetPosition(target).Y>target.ActualHeight/2?1:0):vm.SelectedProfile.Mods.Count; vm.Move(d.Names,destination); RefreshView(d.Names);});
 }
}
