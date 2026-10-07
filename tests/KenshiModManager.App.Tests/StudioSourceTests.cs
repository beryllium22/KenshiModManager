using System.IO;
using System.Xml.Linq;
using Xunit;
namespace KenshiModManager.App.Tests;
public class StudioSourceTests {
 internal static string Repo {get {var d=new DirectoryInfo(AppContext.BaseDirectory); while(d is not null && !File.Exists(Path.Combine(d.FullName,"Directory.Build.props"))) d=d.Parent; return d?.FullName ?? throw new InvalidOperationException("Repository fixtures required");}}
 internal static string Source(string name)=>File.ReadAllText(Path.Combine(Repo,"src","KenshiModManager.App",name));
 [Fact] public void StudioUsesApprovedJadeTokensAndReadableFilenameScale() {
  var doc=XDocument.Parse(Source("Theme.xaml")); XNamespace x="http://schemas.microsoft.com/winfx/2006/xaml";
  var tokens=new Dictionary<string,string> { ["Bg"]="#101719",["Sidebar"]="#131E21",["Surface"]="#172326",["Raised"]="#1E2D31",["Text"]="#EEF4F2",["Secondary"]="#9AAFB4",["Line"]="#304047",["Accent"]="#7CD6B7",["OnPrimary"]="#092D22" };
  foreach(var token in tokens) Assert.Equal(token.Value,doc.Descendants().Single(e=>e.Attribute(x+"Key")?.Value==token.Key).Attribute("Color")?.Value);
  var muted=doc.Descendants().Single(e=>e.Attribute(x+"Key")?.Value=="Muted");
  Assert.Equal("14",muted.Elements().Single(e=>e.Attribute("Property")?.Value=="FontSize").Attribute("Value")?.Value);
  Assert.Contains("PART_Track",Source("Theme.xaml")); Assert.Contains(">64</sys:Double>",Source("Theme.xaml"));
 }
 [Fact] public void StudioFocusesOrderWithOnDemandToolsAndSeparateGameFooter() {
  var doc=XDocument.Parse(Source("MainWindow.xaml")); XNamespace x="http://schemas.microsoft.com/winfx/2006/xaml";
  var names=doc.Descendants().Select(e=>e.Attribute(x+"Name")?.Value).ToArray();
  Assert.Contains("SelectionBar",names); Assert.Contains("ReviewButton",names); Assert.Contains("GameMatchLabel",names);
  Assert.DoesNotContain("DetailsPane",names); Assert.DoesNotContain("DetailsColumn",names);
  Assert.Contains("Add installed mods",Source("MainWindow.xaml")); Assert.Contains("Game setup &amp; backups",Source("MainWindow.xaml"));
  Assert.Equal("Collapsed",doc.Descendants().Single(e=>e.Attribute(x+"Name")?.Value=="SelectionBar").Attribute("Visibility")?.Value);
 }
}
