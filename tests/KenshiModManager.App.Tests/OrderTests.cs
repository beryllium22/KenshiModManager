using KenshiModManager.App;
using KenshiModManager.Core;
using Xunit;
namespace KenshiModManager.App.Tests;
public class OrderTests {
 [Fact] public void Filtered_multi_selection_move_uses_full_order_and_blocks_filtered_drag() {
 var vm=new WorkspaceViewModel(new ManagerState(),[],"a.mod\nb.mod\nc.mod\nd.mod");
 Assert.Equal("c.mod",Assert.Single(vm.Filter("c.mod")).FileName);
 Assert.Throws<InvalidOperationException>(()=>vm.Move(["c.mod"],0,true));
 vm.Move(["d.mod","b.mod"],0);
 Assert.Equal(new[]{"b.mod","d.mod","a.mod","c.mod"},vm.Rows.Select(r=>r.FileName));
 vm.Undo(); Assert.Equal("a.mod",vm.Rows[0].FileName);
 }
 [Fact] public void Disabled_positions_and_relative_selected_order_survive_full_list_moves() {
 var vm=new WorkspaceViewModel(new ManagerState(),[],"a.mod\nb.mod\nc.mod\nd.mod");
 vm.SetEnabled(["b.mod","d.mod"],false);
 Assert.Equal(new[]{1,2,3,4},vm.Rows.Select(r=>r.Position));
 Assert.Equal("a.mod\r\nc.mod\r\n",vm.Export());
 Assert.Equal(new[]{"a.mod","c.mod"},vm.Filter("",enabled:"Enabled").Select(r=>r.FileName));
 vm.Move(["d.mod","b.mod"],0);
 Assert.Equal(new[]{"b.mod","d.mod","a.mod","c.mod"},vm.Rows.Select(r=>r.FileName));
 Assert.Equal(new[]{false,false,true,true},vm.Rows.Select(r=>r.Enabled));
 Assert.Equal(new[]{1,2,3,4},vm.Rows.Select(r=>r.Position));
 Assert.Equal("a.mod\r\nc.mod\r\n",vm.Export());
 vm.Undo(); Assert.Equal(new[]{"a.mod","b.mod","c.mod","d.mod"},vm.Rows.Select(r=>r.FileName));
 Assert.Equal(new[]{true,false,true,false},vm.Rows.Select(r=>r.Enabled));
 }
}
