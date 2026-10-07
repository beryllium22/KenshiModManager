using KenshiModManager.Core;
using Xunit;
namespace KenshiModManager.Core.Tests;
public class CodecTests
{
    [Theory]
    [InlineData("../escape.mod")]
    [InlineData("C:\\mods\\a.mod")]
    [InlineData("bad:name.mod")]
    [InlineData("bad\tname.mod")]
    [InlineData("CON.mod")]
    [InlineData("folder/a.mod")]
    [InlineData("a*.mod")]
    [InlineData("a.mod ")]
    [InlineData("title without extension")]
    public void InvalidNamesFailWithActionableError(string name)
    {
        var error = Assert.Throws<FormatException>(() => LoadOrderCodec.ValidateFileName(name));
        Assert.Contains("filename", error.Message);
        Assert.Throws<FormatException>(() => LoadOrderCodec.Parse(name));
        Assert.Throws<FormatException>(() => LoadOrderCodec.Format([name]));
    }

    [Fact]
    public void NumberedUnicodeLoadOrderRoundTripsExactNames()
    {
        var names = LoadOrderCodec.Parse("\uFEFF1. 日本 語.mod\r\n\r\n2) 123 name.MOD\nplain.mod", removeAuthorNumbering: true);
        Assert.Equal(new[] { "日本 語.mod", "123 name.MOD", "plain.mod" }, names);
        Assert.Equal("日本 語.mod\r\n123 name.MOD\r\nplain.mod\r\n", LoadOrderCodec.Format(names));
        Assert.Equal("", LoadOrderCodec.Format([]));
        var state = new ManagerState { Profiles = [new ModProfile { Mods = [new ProfileMod(names[0])] }] };
        Assert.Equal(1, state.SchemaVersion);
        Assert.True(state.Profiles[0].Mods[0].Enabled);
        Assert.True(Guid.TryParse(state.Profiles[0].Id, out _));
        var mod = new InstalledMod("local:a/a.mod", "a.mod", "a", ModSource.Local, "A");
        Assert.Null(mod.WorkshopId);
    }
}
