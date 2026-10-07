using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
namespace KenshiModManager.App;

/// <summary>Owned, viewport-bounded native Studio dialogs; no game or profile writes.</summary>
internal static class StudioDialogs {
 [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
 internal static Window Create(Window owner,string title,double width=760,double height=580) {
  var dialog=new Window {Owner=owner,Title=title,Width=Math.Min(width,Math.Max(480,owner.ActualWidth-64)),Height=Math.Min(height,Math.Max(400,owner.ActualHeight-64)),MinWidth=480,MinHeight=360,WindowStartupLocation=WindowStartupLocation.CenterOwner,ResizeMode=ResizeMode.CanResize,ShowInTaskbar=false};
  dialog.SetResourceReference(FrameworkElement.StyleProperty,typeof(Window));
  BindCaption(dialog);
  return dialog;
 }
 internal static void BindCaption(Window dialog) {
  dialog.SourceInitialized+=(_,_)=> {var dark=1; var result=DwmSetWindowAttribute(new WindowInteropHelper(dialog).Handle,20,ref dark,sizeof(int)); if(result!=0) System.Diagnostics.Trace.WriteLine($"Dialog dark caption unavailable ({result}).");};
 }
 internal static TextBlock Text(string value,bool muted=false) {
  var text=new TextBlock {Text=value,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12),LineHeight=21};
  if(muted) text.SetResourceReference(TextBlock.ForegroundProperty,"Secondary"); return text;
 }
 internal static TextBox ReadOnly(string value,string name) {
  var text=new TextBox {Text=value,IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,MinHeight=100};
  AutomationProperties.SetName(text,name); return text;
 }
 internal static void Document(Window owner,string title,string explanation,string content) {
  var dialog=Create(owner,title); var root=new DockPanel {Margin=new Thickness(24)}; dialog.Content=root;
  var close=new Button {Content="Close",IsCancel=true,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,16,0,0)}; DockPanel.SetDock(close,Dock.Bottom); root.Children.Add(close);
  var intro=Text(explanation); DockPanel.SetDock(intro,Dock.Top); root.Children.Add(intro); root.Children.Add(ReadOnly(content,title)); dialog.ShowDialog();
 }
 /// <summary>Review never writes. Only an enabled final action after an explicit checkbox returns true.</summary>
 internal static bool Review(Window owner,string title,string summary,string preview,string action,string? blocker) {
  var dialog=Create(owner,title,840,660); var root=new DockPanel {Margin=new Thickness(24)}; dialog.Content=root;
  var footer=new StackPanel(); DockPanel.SetDock(footer,Dock.Bottom); root.Children.Add(footer);
  var reason=Text(blocker ?? "Final confirmation creates an exact backup, checks unchanged bytes and the running-game guard, then verifies the write.",blocker is null); reason.Margin=new Thickness(0,12,0,12); footer.Children.Add(reason);
  var confirmText=Text("I have closed Kenshi and want to write this reviewed game load order."); confirmText.Margin=new Thickness(0);
  var confirm=new CheckBox {Content=confirmText,HorizontalAlignment=HorizontalAlignment.Stretch,Margin=new Thickness(0,0,0,12),IsEnabled=blocker is null}; footer.Children.Add(confirm);
  var buttons=new WrapPanel {HorizontalAlignment=HorizontalAlignment.Right}; footer.Children.Add(buttons);
  var cancel=new Button {Content="Cancel",IsCancel=true}; buttons.Children.Add(cancel);
  var commit=new Button {Content=action,IsEnabled=false}; commit.SetResourceReference(FrameworkElement.StyleProperty,"Primary"); AutomationProperties.SetName(commit,action); ToolTipService.SetShowOnDisabled(commit,true); commit.ToolTip=blocker ?? "Check the explicit confirmation above to enable this final game write."; buttons.Children.Add(commit);
  confirm.Checked+=(_,_)=>commit.IsEnabled=blocker is null; confirm.Unchecked+=(_,_)=>commit.IsEnabled=false;
  commit.Click+=(_,_)=> {if(blocker is null && confirm.IsChecked==true) dialog.DialogResult=true;};
  var intro=Text(summary); DockPanel.SetDock(intro,Dock.Top); root.Children.Add(intro); root.Children.Add(ReadOnly(preview,"Reviewed game load order"));
  return dialog.ShowDialog()==true;
 }
}