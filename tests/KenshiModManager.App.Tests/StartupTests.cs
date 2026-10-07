using KenshiModManager.App;
using KenshiModManager.Core;
using Xunit;
namespace KenshiModManager.App.Tests;
public class StartupTests {
 [Fact] public void SaveSnapshotIsIndependentOfFurtherEdits() {
  var state=new ManagerState(); var p=ProfileStore.Create([],"Before",["A.mod"]); state.Profiles.Add(p);
  var captured=StatePersistence.Snapshot(state); p.Name="After"; p.Mods.Clear();
  Assert.Equal("Before",captured.Profiles[0].Name); Assert.Single(captured.Profiles[0].Mods);
 }
 [Fact] public void AppliedDeleteAndUndoRemainValidForPersistence() {
  var state=new ManagerState(); var p=ProfileStore.Create([],"Applied",["A.mod"]); state.Profiles.Add(p); state.AppliedProfileId=p.Id;
  var vm=new WorkspaceViewModel(state,[]); vm.Delete(); Assert.Null(state.AppliedProfileId); vm.Undo();
  Assert.Single(state.Profiles); Assert.True(state.AppliedProfileId is null || state.Profiles.Any(x=>x.Id==state.AppliedProfileId));
 }
 [Theory] [InlineData("--game-path","--read-only")] [InlineData("--data-dir","--game-path")]
 public void MissingPathDoesNotConsumeAnotherFlag(string key,string next) => Assert.Throws<ArgumentException>(()=>AppOptions.Parse([key,next]));
}
