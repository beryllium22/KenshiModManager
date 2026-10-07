using KenshiModManager.App;
using KenshiModManager.Core;
using Xunit;
namespace KenshiModManager.App.Tests;
public class ProfileInteractionTests {
 [Fact] public void New_switch_and_toggle_are_independent_and_undoable_without_marking_applied() {
 var vm = new WorkspaceViewModel(new ManagerState(), [new("a", "a.mod", "S:/fixture/a.mod", ModSource.Local, "Title")], "a.mod");
 var original=vm.SelectedProfile!; var created=vm.NewProfile("  My collection  ");
 Assert.Equal("My collection",created.Name); Assert.Empty(created.Mods); Assert.Empty(vm.Rows);
 vm.AddMods(["a.mod"],enabled:false); Assert.False(created.Mods[0].Enabled);
 vm.SetEnabled(["a.mod"],true); Assert.True(created.Mods[0].Enabled);
 vm.Undo(); Assert.False(vm.SelectedProfile!.Mods[0].Enabled); vm.Redo(); Assert.True(vm.SelectedProfile!.Mods[0].Enabled);
 vm.SetEnabled(["a.mod"],false); Assert.Single(vm.SelectedProfile!.Mods); Assert.Equal(1,vm.Rows[0].Position);
 vm.RemoveMods(["a.mod"]); Assert.Empty(vm.SelectedProfile.Mods); Assert.Single(vm.Inventory);
 vm.Undo(); Assert.False(Assert.Single(vm.SelectedProfile!.Mods).Enabled);
 vm.Undo(); Assert.True(Assert.Single(vm.SelectedProfile!.Mods).Enabled);
 vm.SelectProfile(original.Id); Assert.True(vm.SelectedProfile!.Mods[0].Enabled);
 vm.SelectProfile(created.Id); Assert.True(vm.SelectedProfile!.Mods[0].Enabled);
 Assert.Null(vm.State.AppliedProfileId);
 }
}
