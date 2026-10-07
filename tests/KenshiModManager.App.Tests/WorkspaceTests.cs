using KenshiModManager.App;
using KenshiModManager.Core;
using Xunit;
namespace KenshiModManager.App.Tests;
public class WorkspaceTests {
 [Fact] public void Imports_only_current_cfg_preserving_missing_without_appending_inventory() {
 var state = new ManagerState(); var vm = new WorkspaceViewModel(state, [new InstalledMod("local:a", "a.mod", "S:/fixture/a.mod", ModSource.Local, "A title"), new InstalledMod("local:extra", "extra.mod", "S:/fixture/extra.mod", ModSource.Local, "Extra")], "missing.mod\r\na.mod\r\n");
 Assert.Equal("Current Kenshi Setup", vm.SelectedProfile!.Name);
 Assert.Equal(new[]{"missing.mod", "a.mod"}, vm.SelectedProfile.Mods.Select(m=>m.FileName));
 Assert.Equal("Missing", vm.Rows[0].Status); Assert.Equal("A title", vm.Rows[1].Title);
 Assert.False(vm.CanApply); Assert.Null(state.AppliedProfileId);
 }
}
