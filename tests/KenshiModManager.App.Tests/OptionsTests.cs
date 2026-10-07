using KenshiModManager.App;
using Xunit;
namespace KenshiModManager.App.Tests;
public class OptionsTests {
 [Fact] public void Parses_both_flag_forms_and_read_only() {
 var o = AppOptions.Parse(["--game-path", "S:/fixture/game", "--data-dir=S:/fixture/data", "--read-only"]);
 Assert.Equal("S:/fixture/game", o.GamePath); Assert.Equal("S:/fixture/data", o.DataDirectory); Assert.True(o.ReadOnly);
 }
}
