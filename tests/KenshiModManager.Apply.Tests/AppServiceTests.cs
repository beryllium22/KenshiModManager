using KenshiModManager.App;
using KenshiModManager.Core;
using Xunit;

namespace KenshiModManager.Apply.Tests;

public sealed class AppServiceTests : IDisposable
{
    private readonly string root = TestFixture.CreateRoot("service");
    public AppServiceTests() => Directory.CreateDirectory(Path.Combine(root, "game", "data"));
    public void Dispose() => Directory.Delete(root, true);

    [Fact]
    public void App_service_reads_configuration_from_the_selected_game()
    {
        var game = Path.Combine(root,"game"); var cfg = Path.Combine(game,"data","mods.cfg");
        File.WriteAllText(cfg,"A.mod\r\n");
        var service = new GameWriteService(game, Path.Combine(root,"state"), false);
        Assert.Equal(Path.GetFullPath(cfg), service.Read().Path);
        Assert.Equal(File.ReadAllBytes(cfg), service.Read().Bytes);
    }

    [Fact]
    public void App_service_applies_fixture_profile_and_guards_running_game()
    {
        var game = Path.Combine(root,"game"); var cfg = Path.Combine(game,"data","mods.cfg");
        File.WriteAllText(cfg,"Old.mod\r\n");
        var mod = Path.Combine(root,"A.mod"); File.WriteAllBytes(mod,[]);
        InstalledMod[] inventory = [new("local:A","A.mod",mod,ModSource.Local,"A")];
        var profile = new ModProfile { Mods = [new("A.mod")] };
        var service = new GameWriteService(game,Path.Combine(root,"state"),false);
        Assert.Throws<InvalidOperationException>(() => service.Apply(profile,inventory,service.Read(),true));
        var result = service.Apply(profile,inventory,service.Read(),false);
        Assert.Equal("A.mod\r\n",File.ReadAllText(cfg));
        Assert.Equal("Old.mod\r\n",File.ReadAllText(result.BackupPath));
        Assert.StartsWith(Path.GetFullPath(Path.Combine(root,"state","backups")),result.BackupPath);
    }

    [Fact]
    public void App_service_restore_backs_up_current_and_preserves_exact_bytes()
    {
        var game = Path.Combine(root,"game"); var cfg = Path.Combine(game,"data","mods.cfg");
        File.WriteAllText(cfg,"Current.mod\r\n");
        var backup = Path.Combine(root,"original.cfg");
        byte[] bytes = [0xEF,0xBB,0xBF,..System.Text.Encoding.UTF8.GetBytes("Restored.mod\n")];
        File.WriteAllBytes(backup,bytes);
        var service = new GameWriteService(game,Path.Combine(root,"state"),false);
        Assert.Throws<InvalidOperationException>(() => service.Restore(backup,service.Read(),true));
        var result = service.Restore(backup,service.Read(),false);
        Assert.Equal(bytes,File.ReadAllBytes(cfg));
        Assert.Equal("Current.mod\r\n",File.ReadAllText(result.BackupPath));
    }

    [Fact]
    public void Read_only_app_blocks_apply_and_restore_without_creating_backups()
    {
        var game = Path.Combine(root, "game"); var cfg = Path.Combine(game, "data", "mods.cfg");
        File.WriteAllText(cfg, "Existing.mod\r\n");
        var before = new SafeLoadOrderWriter().Read(cfg);
        var service = new GameWriteService(game, Path.Combine(root,"state"), true);
        Assert.Throws<InvalidOperationException>(() => service.Apply(new ModProfile(), [], before, false));
        Assert.Throws<InvalidOperationException>(() => service.Restore(cfg, before, false));
        Assert.Equal(before.Bytes, File.ReadAllBytes(cfg));
        Assert.False(Directory.Exists(Path.Combine(root, "state")));
    }
}
