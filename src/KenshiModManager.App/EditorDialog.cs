using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using Microsoft.Win32;
namespace KenshiModManager.App;
public sealed record DialogField(string Label,string Value="",bool Multiline=false,bool Folder=false);
/// <summary>Native reusable modal editor. Validation errors retain typed values.</summary>
public static class EditorDialog {
 public static string[]? Show(Window owner,string title,DialogField[] fields,string accept="Save",Func<string[],string?>? validate=null,string? explanation=null,CheckBox? option=null) {
  var window=StudioDialogs.Create(owner,title,660,fields.Any(f=>f.Multiline)?620:440);
  var root=new DockPanel { Margin=new Thickness(24) }; window.Content=root;
  var buttons=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,16,0,0) }; DockPanel.SetDock(buttons,Dock.Bottom); root.Children.Add(buttons);
  var cancel=new Button { Content="Cancel",IsCancel=true }; var ok=new Button {Content=accept,IsDefault=true}; ok.SetResourceReference(FrameworkElement.StyleProperty,"Primary"); buttons.Children.Add(cancel); buttons.Children.Add(ok);
  var panel=new StackPanel(); root.Children.Add(new ScrollViewer {Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
  if(explanation is not null) panel.Children.Add(new TextBlock {Text=explanation,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,16)});
  var inputs=new List<TextBox>();
  foreach(var field in fields) {
   panel.Children.Add(new TextBlock {Text=field.Label,Margin=new Thickness(0,8,0,6)});
   var input=new TextBox {Text=field.Value,AcceptsReturn=field.Multiline,TextWrapping=field.Multiline?TextWrapping.Wrap:TextWrapping.NoWrap,VerticalScrollBarVisibility=field.Multiline?ScrollBarVisibility.Auto:ScrollBarVisibility.Disabled,MinHeight=field.Multiline?120:34,MaxHeight=field.Multiline?240:34}; AutomationProperties.SetName(input,field.Label); inputs.Add(input);
   var row=new DockPanel(); if(field.Folder) {var browse=new Button {Content="Browse…",Margin=new Thickness(8,0,0,0)}; DockPanel.SetDock(browse,Dock.Right); row.Children.Add(browse); browse.Click+=(_,_)=> {var picker=new OpenFolderDialog {Title=field.Label}; if(picker.ShowDialog(window)==true) input.Text=picker.FolderName;};} row.Children.Add(input); panel.Children.Add(row);
  }
  if(option is not null) { option.HorizontalAlignment=HorizontalAlignment.Stretch; option.Margin=new Thickness(0,12,0,0); panel.Children.Add(option); }
  var error=new TextBlock {TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,0)}; error.SetResourceReference(TextBlock.ForegroundProperty,"Error"); panel.Children.Add(error);
  string[]? result=null;
  ok.Click+=(_,_)=> {try { var values=inputs.Select(x=>x.Text).ToArray(); var message=validate?.Invoke(values); if(message is not null) {error.Text=message; return;} result=values; window.DialogResult=true;} catch(Exception problem) {error.Text=problem.Message;} };
  window.Loaded+=(_,_)=> {if(inputs.Count>0) { inputs[0].Focus(); if(!fields[0].Multiline) inputs[0].SelectAll(); }};
  window.ShowDialog(); return result;
 }
}
