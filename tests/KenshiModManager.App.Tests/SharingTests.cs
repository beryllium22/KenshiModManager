using KenshiModManager.App;
using KenshiModManager.Core;
using Xunit;
namespace KenshiModManager.App.Tests;
public class SharingTests {
 [Fact] public void Rename_duplicate_delete_recovery_and_versioned_share_preserve_independence() {
 var vm=new WorkspaceViewModel(new ManagerState(),[],"a.mod\nb.mod");
 var original=vm.SelectedProfile!; vm.Rename("  Original  "); Assert.Equal("Original",original.Name);
 var copy=vm.Duplicate("Collection"); vm.SetEnabled(["a.mod"],false);
 Assert.True(original.Mods[0].Enabled); Assert.NotEqual(original.Id,copy.Id);
 vm.SetMetadata("https://steamcommunity.com/sharedfiles/filedetails/?id=123","Author order");
 var json=vm.Export(true); var imported=vm.Import(json,json:true);
 Assert.Equal("Author order", imported.Notes); Assert.False(imported.Mods[0].Enabled);
 Assert.NotEqual(copy.Id,imported.Id); vm.Delete(); Assert.DoesNotContain(vm.Profiles,p=>p.Id==imported.Id);
 vm.Undo(); Assert.Contains(vm.Profiles,p=>p.Id==imported.Id);
 var pasted=vm.Import("2. b.mod\n1. missing.mod", " Pasted ",removeAuthorNumbering:true);
 Assert.Equal("Pasted",pasted.Name); Assert.Equal("b.mod\r\nmissing.mod\r\n",vm.Export());
 }
 [Fact] public void Import_preserves_literal_missing_entries_without_absorbing_inventory_on_scan_or_undo() {
 var vm=new WorkspaceViewModel(new ManagerState(),[new("local:extra","extra.mod","S:/fixture/extra.mod",ModSource.Local,"Extra")],"original.mod");
 var original=vm.SelectedProfile!;
 var pasted=vm.Import("B.MOD\nMissing.MOD","Author supplied");
 var expected=new[]{new ProfileMod("B.MOD"),new ProfileMod("Missing.MOD")};
 Assert.Equal(expected,pasted.Mods); Assert.Equal("B.MOD\r\nMissing.MOD\r\n",vm.Export());
 vm.ReplaceInventory([new("local:b","b.mod","S:/fixture/b.mod",ModSource.Local,"B"),new("local:new","new.mod","S:/fixture/new.mod",ModSource.Local,"New")]);
 Assert.Equal(expected,pasted.Mods); Assert.Equal("Missing",vm.Rows[1].Status);
 vm.SetMetadata("https://example.com/order","Author notes"); vm.SetEnabled(["Missing.MOD"],false); vm.Undo();
 Assert.Equal(expected,vm.SelectedProfile!.Mods); Assert.Equal("Author notes",vm.SelectedProfile.Notes);
 vm.SelectProfile(original.Id); Assert.Equal(new[]{new ProfileMod("original.mod")},vm.SelectedProfile!.Mods);
 vm.SelectProfile(pasted.Id); Assert.Equal(expected,vm.SelectedProfile!.Mods); Assert.Null(vm.State.AppliedProfileId);
 }
 [Fact] public void Mixed_case_added_member_exports_literal_filename_and_portable_reference_without_mutating_scanner_identity() {
 var vm=new WorkspaceViewModel(new ManagerState(),[new("workshop:42/b.mod","b.mod","S:/fixture/b.mod",ModSource.Workshop,"B","42")],"");
 vm.NewProfile("Case-preserving export"); vm.AddMods(["B.MOD"],false);
 var original=vm.SelectedProfile!;
 var imported=ProfileStore.ImportProfile(vm.Export(true),[]);
 Assert.Equal(new ProfileMod("B.MOD",false,"workshop:42/B.MOD"),Assert.Single(imported.Mods));
 Assert.Equal(new ProfileMod("B.MOD",false,"workshop:42/b.mod"),Assert.Single(original.Mods));
 Assert.Same(original,vm.SelectedProfile); Assert.NotEqual(original.Id,imported.Id);
 }
 [Theory]
 [InlineData("workshop:42/Other.mod")]
 [InlineData("workshop:forty-two/b.mod")]
 [InlineData("workshop:-42/b.mod")]
 [InlineData("workshop:42/../../B.MOD")]
 public void Portable_export_still_rejects_malformed_or_different_member_references(string reference) {
 var profile=new ModProfile {Name="Invalid reference",Mods=[new("B.MOD",true,reference)]};
 var vm=new WorkspaceViewModel(new ManagerState {Profiles=[profile],SelectedProfileId=profile.Id},[],"");
 Assert.Throws<System.IO.InvalidDataException>(()=>vm.Export(true));
 Assert.Equal(new ProfileMod("B.MOD",true,reference),Assert.Single(profile.Mods));
 }
}
