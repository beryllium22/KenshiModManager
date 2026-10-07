using System.Windows;
using System.Windows.Controls;
using KenshiModManager.Core;
namespace KenshiModManager.App;

public partial class MainWindow {
 private bool PreviewImport(string text,string name,bool json,bool removeAuthorNumbering=false) {
  if(vm is null) return false;
  var preview=json?ProfileStore.ImportProfile(text,vm.State.Profiles):ProfileStore.Create(vm.State.Profiles,name,LoadOrderCodec.Parse(text,removeAuthorNumbering));
  var lookup=vm.Inventory.ToLookup(m=>m.FileName,StringComparer.OrdinalIgnoreCase);
  var rows=preview.Mods.Select((m,index)=>$"{index+1}. {(m.Enabled?"ON ":"OFF")}  {m.FileName}  [{(lookup[m.FileName].Count() switch {0=>"Missing",1=>"Installed",_=>"Ambiguous"})}]");
  var dialog=StudioDialogs.Create(this,"Import preview — NEW profile",820,650); var root=new DockPanel {Margin=new Thickness(24)}; dialog.Content=root;
  var buttons=new WrapPanel {HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,16,0,0)}; DockPanel.SetDock(buttons,Dock.Bottom); root.Children.Add(buttons);
  buttons.Children.Add(new Button {Content="Cancel",IsCancel=true}); var create=new Button {Content="Create new profile",IsDefault=true}; create.SetResourceReference(FrameworkElement.StyleProperty,"Primary"); buttons.Children.Add(create); create.Click+=(_,_)=>dialog.DialogResult=true;
  var intro=StudioDialogs.Text($"Create “{preview.Name}” with {preview.Mods.Count} members ({preview.Mods.Count(m=>m.Enabled)} enabled). No edits to the selected profile; no game writes or subscriptions. Missing and ambiguous entries remain explicit."); DockPanel.SetDock(intro,Dock.Top); root.Children.Add(intro);
  root.Children.Add(StudioDialogs.ReadOnly(string.Join("\n",rows)+$"\n\nSource: {preview.SourceUrl ?? "None"}\nNotes: {preview.Notes ?? "None"}","Imported filenames, enabled state, source and notes")); return dialog.ShowDialog()==true;
 }
}