using System.IO;
using System.Text;
using KenshiModManager.App;
using KenshiModManager.Core;
using Xunit;
namespace KenshiModManager.App.Tests;
public class GameWriteTests {
 [Fact] public void Read_only_blocks_apply_and_restore_before_any_write() {
 var root=Path.Combine(StudioSourceTests.Repo,".hermes","test-data","kmm-ui-"+Guid.NewGuid());
 Directory.CreateDirectory(Path.Combine(root,"data"));
 try {
 var cfg=Path.Combine(root,"data","mods.cfg"); var original=Encoding.UTF8.GetBytes("missing.mod\r\n"); File.WriteAllBytes(cfg,original);
 var service=new GameWriteService(root,Path.Combine(root,"manager"),true); var snapshot=service.Read();
 Assert.Throws<InvalidOperationException>(()=>service.Apply(new ModProfile(),[],snapshot,false));
 Assert.Throws<InvalidOperationException>(()=>service.Restore(cfg,snapshot,false));
 Assert.Equal(original,File.ReadAllBytes(cfg)); Assert.False(Directory.Exists(Path.Combine(root,"manager")));
 } finally {Directory.Delete(root,true);}
 }
}
