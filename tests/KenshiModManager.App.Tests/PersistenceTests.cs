using System.IO;
using KenshiModManager.App;
using KenshiModManager.Core;
using Xunit;
namespace KenshiModManager.App.Tests;
public class PersistenceTests {
 private static string Fixture() {
  var root=new DirectoryInfo(AppContext.BaseDirectory); while(root is not null && !File.Exists(Path.Combine(root.FullName,"Directory.Build.props"))) root=root.Parent;
  if(root is null) throw new InvalidOperationException("Repository fixture root not found.");
  var path=Path.Combine(root.FullName,".hermes","test-data","state-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path); return path;
 }
 [Fact] public async Task CorruptStateIsNotResetOrOverwritten() {
  var data=Fixture(); var file=Path.Combine(data,"state.json"); const string corrupt="{broken user state"; await File.WriteAllTextAsync(file,corrupt); var store=new StatePersistence(data);
  Assert.Throws<InvalidDataException>(()=>store.Load()); await Assert.ThrowsAsync<InvalidDataException>(()=>store.SaveAsync(new ManagerState())); Assert.Equal(corrupt,await File.ReadAllTextAsync(file));
 }
 [Fact] public async Task QueuedAutosavesPersistSelectedButNotAppliedAndIndependentProfiles() {
  var data=Fixture(); var store=new StatePersistence(data); var vm=new WorkspaceViewModel(new ManagerState(),[],"A.mod"); var original=vm.SelectedProfile!.Id;
  var first=store.SaveAsync(StatePersistence.Snapshot(vm.State)); vm.Duplicate("Second"); vm.SetEnabled(["A.mod"],false); var second=store.SaveAsync(StatePersistence.Snapshot(vm.State)); await Task.WhenAll(first,second);
  var saved=store.Load(); Assert.Equal(vm.SelectedProfile!.Id,saved.SelectedProfileId); Assert.Null(saved.AppliedProfileId); Assert.True(saved.Profiles.First(p=>p.Id==original).Mods[0].Enabled); Assert.False(saved.Profiles.First(p=>p.Id==saved.SelectedProfileId).Mods[0].Enabled);
 }
 [Fact] public void RawImportsPreserveNumberedLiteralNamesUnlessAuthorOptionIsExplicit() {
  var vm=new WorkspaceViewModel(new ManagerState(),[],"123. Real.mod"); Assert.Equal("123. Real.mod",vm.Rows[0].FileName);
  Assert.Equal("123. Real.mod",vm.Import("123. Real.mod").Mods[0].FileName); Assert.Equal("Real.mod",vm.Import("123. Real.mod",removeAuthorNumbering:true).Mods[0].FileName);
 }
}
