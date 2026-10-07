using KenshiModManager.Core;
using Xunit;
namespace KenshiModManager.Core.Tests;
public sealed class LiteralCodecTests
{
    [Fact]
    public void Raw_cfg_preserves_a_legitimate_numbered_filename_without_normalization()
    {
        var names = LoadOrderCodec.Parse("123. Real Mod.mod\r\n2) Another Real.mod\r\n");
        Assert.Equal(new[]{"123. Real Mod.mod","2) Another Real.mod"},names);
    }
}
