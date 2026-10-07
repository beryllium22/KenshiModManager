using System.IO;
using System.Text.Json;
using KenshiModManager.App;
using KenshiModManager.Core;
using Xunit;
namespace KenshiModManager.App.Tests;
public class MembershipTests {
 private static InstalledMod Installed(string name, string id = "local:one") => new(id, name, "S:/fixture/" + name, ModSource.Local, "Title: " + name);
 [Fact] public void Explicit_add_appends_literal_members_in_read_only_and_undo_restores_original() {
 var existing = new ModProfile { Name = "Existing", Mods = [new("A.MOD", false, "local:old")], Notes = "Keep me" };
 var vm = new WorkspaceViewModel(new ManagerState { Profiles = [existing] }, [Installed("a.mod"), Installed("b.mod", "workshop:42/b.mod")]) { ReadOnly = true };
 var add = typeof(WorkspaceViewModel).GetMethod("AddMods", [typeof(IEnumerable<string>), typeof(bool)]);
 Assert.NotNull(add); Assert.Equal(typeof(void), add.ReturnType);
 Assert.True(add.GetParameters()[1].IsOptional); Assert.Equal(true, add.GetParameters()[1].DefaultValue);
 add.Invoke(vm, new object[] { new[] { "B.MOD" }, true });
 Assert.Equal(new[] { new ProfileMod("A.MOD", false, "local:old"), new ProfileMod("B.MOD", true, "workshop:42/b.mod") }, existing.Mods);
 Assert.Equal("Keep me", existing.Notes); Assert.False(vm.CanApply); Assert.True(vm.CanUndo);
 vm.Undo(); Assert.Equal(new[] { new ProfileMod("A.MOD", false, "local:old") }, vm.SelectedProfile!.Mods);
 vm.Redo(); Assert.Equal("B.MOD", vm.SelectedProfile!.Mods[1].FileName);
 }
 [Fact] public void Add_deduplicates_case_insensitively_without_enabling_or_replacing_existing_members() {
 var existing = new ModProfile { Name = "Existing", Mods = [new("A.MOD", false, "local:historic"), new("Missing.mod", false, "workshop:old/Missing.mod")] };
 var vm = new WorkspaceViewModel(new ManagerState { Profiles = [existing] }, [Installed("a.mod"), Installed("b.mod", "local:b"), Installed("c.mod", "local:c")]);
 vm.AddMods(["a.mod", "B.MOD", "b.mod", "C.MOD"]);
 Assert.Equal(new[] { new ProfileMod("A.MOD", false, "local:historic"), new ProfileMod("Missing.mod", false, "workshop:old/Missing.mod"), new ProfileMod("B.MOD", true, "local:b"), new ProfileMod("C.MOD", true, "local:c") }, existing.Mods);
 vm.Undo(); Assert.True(vm.CanRedo);
 var changes = 0; vm.Changed += () => changes++;
 vm.AddMods(["a.mod", "Missing.mod"]);
 Assert.Equal(0, changes); Assert.True(vm.CanRedo); Assert.False(vm.CanUndo);
 Assert.Equal(new[] { new ProfileMod("A.MOD", false, "local:historic"), new ProfileMod("Missing.mod", false, "workshop:old/Missing.mod") }, vm.SelectedProfile!.Mods);
 }
 [Theory]
 [InlineData("bad/name.mod", typeof(FormatException), "exact filename")]
 [InlineData("absent.mod", typeof(InvalidOperationException), "Install")]
 [InlineData("ambiguous.mod", typeof(InvalidOperationException), "multiple")]
 [InlineData(null, typeof(FormatException), "exact filename")]
 public void Add_rejects_an_invalid_batch_atomically_with_actionable_error(string? invalid, Type exceptionType, string reason) {
 var vm = new WorkspaceViewModel(new ManagerState(), [Installed("good.mod"), Installed("bad/name.mod"), Installed("ambiguous.mod"), Installed("AMBIGUOUS.MOD", "local:other")]);
 vm.NewProfile("Other"); vm.Undo();
 var before = JsonSerializer.Serialize(vm.State); var changes = 0; vm.Changed += () => changes++;
 var error = Record.Exception(() => vm.AddMods(new[] { "good.mod", invalid! }));
 Assert.NotNull(error); Assert.IsType(exceptionType, error); Assert.Contains(reason, error.Message);
 if(invalid is not null) Assert.Contains(invalid, error.Message);
 Assert.Equal(before, JsonSerializer.Serialize(vm.State)); Assert.Empty(vm.Rows);
 Assert.False(vm.CanUndo); Assert.True(vm.CanRedo); Assert.Equal(0, changes);
 }
 [Fact] public void Explicit_remove_is_profile_only_in_read_only_and_undo_restores_flags_positions_and_references() {
 var first = new ModProfile { Name = "First", Mods = [new("a.mod", true, "local:a"), new("missing.mod", false, "workshop:old/missing.mod"), new("b.mod", false, "local:b")] };
 var other = new ModProfile { Name = "Other", Mods = [new("missing.mod", true, "workshop:other/missing.mod")] };
 var inventory = new[] { Installed("a.mod", "local:a"), Installed("b.mod", "local:b") };
 var state = new ManagerState { Profiles = [first, other], SelectedProfileId = first.Id, AppliedProfileId = other.Id };
 var vm = new WorkspaceViewModel(state, inventory) { ReadOnly = true }; var before = JsonSerializer.Serialize(state);
 var remove = typeof(WorkspaceViewModel).GetMethod("RemoveMods", [typeof(IEnumerable<string>)]);
 Assert.NotNull(remove); Assert.Equal(typeof(void), remove.ReturnType);
 remove.Invoke(vm, new object[] { new[] { "MISSING.MOD", "missing.mod", "absent.mod" } });
 Assert.Equal(new[] { first.Mods[0], new ProfileMod("b.mod", false, "local:b") }, vm.SelectedProfile!.Mods);
 Assert.Equal(new[] {1,2}, vm.Rows.Select(r => r.Position)); Assert.Same(inventory, vm.Inventory);
 Assert.Single(other.Mods); Assert.Equal(other.Id, state.AppliedProfileId);
 vm.Undo(); Assert.Equal(before, JsonSerializer.Serialize(state));
 vm.Redo(); Assert.Equal(new[] { "a.mod", "b.mod" }, vm.SelectedProfile!.Mods.Select(m => m.FileName));
 }
 [Fact] public void Rows_expose_typed_installation_state_and_present_Installed_instead_of_Ready() {
 var vm = new WorkspaceViewModel(new ManagerState(), [Installed("a.mod"), Installed("ambiguous.mod"), Installed("AMBIGUOUS.MOD", "local:other")], "a.mod\nmissing.mod\nambiguous.mod");
 var property = typeof(ModRow).GetProperty("InstallationState"); Assert.NotNull(property); Assert.True(property.PropertyType.IsEnum);
 Assert.Equal("ModInstallationState", property.PropertyType.Name);
 Assert.Equal(new[] { "Installed", "Missing", "Ambiguous" }, Enum.GetNames(property.PropertyType));
 Assert.Equal(new[] { "Installed", "Missing", "Ambiguous" }, vm.Rows.Select(r => property.GetValue(r)!.ToString()));
 Assert.Equal(new[] { "Installed", "Missing", "Ambiguous" }, vm.Rows.Select(r => r.Status));
 Assert.False(vm.CanApply); vm.SetEnabled(["missing.mod", "ambiguous.mod"], false); Assert.True(vm.CanApply);
 Assert.Single(vm.Filter("", status: "Installed")); Assert.Single(vm.Filter("", status: "Missing")); Assert.Single(vm.Filter("", status: "Ambiguous"));
 }
 [Theory]
 [InlineData(ModInstallationState.Installed, "Missing", true)]
 [InlineData(ModInstallationState.Missing, "Installed", false)]
 [InlineData(ModInstallationState.Ambiguous, "Ready", false)]
 public void CanApply_uses_typed_state_not_the_presentation_label(ModInstallationState installation, string label, bool canApply) {
 var vm = new WorkspaceViewModel(new ManagerState(), [], "a.mod");
 vm.Rows.Clear(); vm.Rows.Add(new ModRow { FileName = "a.mod", Title = "A", Status = label, InstallationState = installation, Source = "Local", Position = 1, Enabled = true });
 Assert.Equal(canApply, vm.CanApply);
 vm.ReadOnly = true; Assert.False(vm.CanApply);
 }
 [Fact] public void Failed_enumeration_and_no_op_batches_leave_history_membership_and_notifications_unchanged() {
 var vm = new WorkspaceViewModel(new ManagerState(), [Installed("a.mod")]);
 vm.AddMods(["a.mod"], enabled:false); vm.Undo();
 var baseline = JsonSerializer.Serialize(vm.State); var changes = 0; vm.Changed += () => changes++;
 IEnumerable<string> FailingBatch() { yield return "a.mod"; throw new IOException("Fixture enumeration failed."); }
 Assert.Throws<IOException>(() => vm.AddMods(FailingBatch()));
 Assert.Throws<IOException>(() => vm.RemoveMods(FailingBatch()));
 Assert.Throws<ArgumentNullException>(() => vm.AddMods(null!));
 Assert.Throws<ArgumentNullException>(() => vm.RemoveMods(null!));
 vm.AddMods([]); vm.RemoveMods([]); vm.RemoveMods(["absent.mod"]);
 Assert.Equal(baseline, JsonSerializer.Serialize(vm.State)); Assert.True(vm.CanRedo); Assert.False(vm.CanUndo); Assert.Equal(0, changes);
 vm.Redo(); Assert.False(Assert.Single(vm.SelectedProfile!.Mods).Enabled);
 }
 [Fact] public void Add_and_remove_require_a_selected_profile_without_mutating_inventory() {
 var vm = new WorkspaceViewModel(new ManagerState(), [Installed("a.mod")]);
 vm.Delete(); var before = JsonSerializer.Serialize(vm.State);
 Assert.Throws<InvalidOperationException>(() => vm.AddMods(["a.mod"]));
 Assert.Throws<InvalidOperationException>(() => vm.RemoveMods(["a.mod"]));
 Assert.Equal(before, JsonSerializer.Serialize(vm.State)); Assert.Single(vm.Inventory);
 }
 [Fact] public void Construction_selection_scan_and_undo_projection_retain_lossless_schema_one_membership() {
 var legacy = new ModProfile { Name = "Legacy", SourceUrl = "https://example.com/order", Notes = "Literal author notes", Mods = [new("A.MOD", false, "local:historic"), new("Missing.mod", true, "workshop:123/Missing.mod"), new("tail.mod", false)] };
 var empty = new ModProfile { Name = "Empty" };
 var state = new ManagerState { Profiles = [legacy, empty], SelectedProfileId = legacy.Id, AppliedProfileId = legacy.Id, GamePath = "S:/fixture/game", WorkshopPaths = ["S:/fixture/workshop"] };
 var baseline = JsonSerializer.Serialize(state);
 var vm = new WorkspaceViewModel(state, [Installed("a.mod"), Installed("extra.mod")]);
 Assert.Equal(baseline, JsonSerializer.Serialize(state));
 vm.SelectProfile(empty.Id); Assert.Empty(vm.Rows); Assert.Empty(empty.Mods);
 vm.SelectProfile(legacy.Id); Assert.Equal(baseline, JsonSerializer.Serialize(state));
 vm.ReplaceInventory([Installed("a.mod", "local:new"), Installed("new.mod")]);
 Assert.Equal(baseline, JsonSerializer.Serialize(state));
 vm.SetEnabled(["Missing.mod"], false); vm.Undo();
 Assert.Equal(baseline, JsonSerializer.Serialize(state));
 Assert.Equal(new[] {1,2,3}, vm.Rows.Select(r => r.Position));
 Assert.Equal(legacy.Id, state.AppliedProfileId);
 var created = vm.NewProfile("Truly new"); Assert.Empty(created.Mods); Assert.Empty(vm.Rows);
 vm.Undo(); Assert.Equal(baseline, JsonSerializer.Serialize(state));
 }
}
