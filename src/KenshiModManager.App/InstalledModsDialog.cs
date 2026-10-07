using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using KenshiModManager.Core;
namespace KenshiModManager.App;

/// <summary>On-demand library picker. A checked item is a membership decision, never a Steam action.</summary>
public sealed class InstalledModsDialog : Window {
 private sealed record Candidate(string FileName,string Title,string Source,string Status,bool CanAdd);
 public InstalledModsDialog(Window owner,WorkspaceViewModel workspace) {
  Owner=owner; Title="Add installed mods"; Width=Math.Min(880,Math.Max(480,owner.ActualWidth-64)); Height=Math.Min(640,Math.Max(400,owner.ActualHeight-64)); MinWidth=480; MinHeight=360; WindowStartupLocation=WindowStartupLocation.CenterOwner; ShowInTaskbar=false;
  SetResourceReference(FrameworkElement.StyleProperty,typeof(Window)); StudioDialogs.BindCaption(this);
  var members=(workspace.SelectedProfile?.Mods ?? []).Select(m=>m.FileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
  var candidates=workspace.Inventory.GroupBy(m=>m.FileName,StringComparer.OrdinalIgnoreCase).Select(g=> {
   var first=g.First(); var ambiguous=g.Count()>1; var present=members.Contains(first.FileName);
   return new Candidate(first.FileName,first.DisplayName,string.Join(" / ",g.Select(m=>m.Source.ToString()).Distinct()),present?"In profile":ambiguous?"Ambiguous":"Available",!present&&!ambiguous);
  }).OrderBy(c=>c.Title,StringComparer.OrdinalIgnoreCase).ThenBy(c=>c.FileName,StringComparer.OrdinalIgnoreCase).ToArray();
  var root=new DockPanel {Margin=new Thickness(24)}; Content=root;
  var footer=new StackPanel(); DockPanel.SetDock(footer,Dock.Bottom); root.Children.Add(footer);
  var status=StudioDialogs.Text("Select installed filenames to add. Ctrl/Shift selects multiple rows.",true); status.Margin=new Thickness(0,12,0,12); footer.Children.Add(status);
  var buttons=new WrapPanel {HorizontalAlignment=HorizontalAlignment.Right}; footer.Children.Add(buttons);
  buttons.Children.Add(new Button {Content="Cancel",IsCancel=true}); var disabled=new Button {Content="Add disabled",IsEnabled=false}; var enabled=new Button {Content="Add enabled",IsEnabled=false}; enabled.SetResourceReference(FrameworkElement.StyleProperty,"Primary"); buttons.Children.Add(disabled); buttons.Children.Add(enabled);
  var header=new StackPanel(); DockPanel.SetDock(header,Dock.Top); root.Children.Add(header);
  header.Children.Add(StudioDialogs.Text($"Add members to “{workspace.SelectedProfile?.Name}”. Existing members and ambiguous filenames cannot be added here. Nothing downloads and nothing writes to Kenshi."));
  var search=new TextBox {Margin=new Thickness(0,0,0,12)}; AutomationProperties.SetName(search,"Search installed filenames, titles or sources"); header.Children.Add(search);
  var grid=new DataGrid {AutoGenerateColumns=false,IsReadOnly=true,SelectionMode=DataGridSelectionMode.Extended,SelectionUnit=DataGridSelectionUnit.FullRow,CanUserAddRows=false,CanUserDeleteRows=false,CanUserSortColumns=true}; AutomationProperties.SetName(grid,"Installed mods available to add"); root.Children.Add(grid);
  var cellText=new Style(typeof(TextBlock),TryFindResource(typeof(TextBlock)) as Style);
  cellText.Setters.Add(new Setter(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis));
  cellText.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center));
  cellText.Setters.Add(new Setter(FrameworkElement.ToolTipProperty,new Binding("FileName")));
  var titleText=new Style(typeof(TextBlock),cellText);
  titleText.Setters.Add(new Setter(FrameworkElement.ToolTipProperty,new Binding("Title")));
  var cellStyle=new Style(typeof(DataGridCell),TryFindResource(typeof(DataGridCell)) as Style);
  cellStyle.Setters.Add(new Setter(UIElement.ClipToBoundsProperty,true)); grid.CellStyle=cellStyle;
  grid.Columns.Add(new DataGridTextColumn {Header="TITLE",Binding=new Binding("Title"),ElementStyle=titleText,Width=new DataGridLength(1,DataGridLengthUnitType.Star),MinWidth=130});
  grid.Columns.Add(new DataGridTextColumn {Header="EXACT FILENAME",Binding=new Binding("FileName"),ElementStyle=cellText,Width=new DataGridLength(1,DataGridLengthUnitType.Star),MinWidth=160});
  grid.Columns.Add(new DataGridTextColumn {Header="SOURCE",Binding=new Binding("Source"),ElementStyle=cellText,Width=105}); grid.Columns.Add(new DataGridTextColumn {Header="STATE",Binding=new Binding("Status"),ElementStyle=cellText,Width=115});
  var view=new ListCollectionView(candidates); grid.ItemsSource=view;
  search.TextChanged+=(_,_)=> {view.Filter=item=>item is Candidate c && $"{c.FileName} {c.Title} {c.Source} {c.Status}".Contains(search.Text,StringComparison.OrdinalIgnoreCase); view.Refresh();};
  void Selection() {var rows=grid.SelectedItems.OfType<Candidate>().ToArray(); var count=rows.Count(c=>c.CanAdd); status.Text=$"{count} available selected · {rows.Length-count} existing/ambiguous skipped · additions go to the end of the profile"; enabled.IsEnabled=disabled.IsEnabled=count>0;}
  grid.SelectionChanged+=(_,_)=>Selection();
  void Add(bool on) {try {var names=grid.SelectedItems.OfType<Candidate>().Where(c=>c.CanAdd).Select(c=>c.FileName).ToArray(); if(names.Length==0) return; workspace.AddMods(names,on); DialogResult=true;} catch(Exception error) {status.Text=error.Message;}}
  enabled.Click+=(_,_)=>Add(true); disabled.Click+=(_,_)=>Add(false); Loaded+=(_,_)=>search.Focus();
 }
}