using KenshiModManager.Core;
using Xunit;
namespace KenshiModManager.Core.Tests;
public class EditorTests
{
    [Fact]
    public void BatchEnableAndInvalidEditsAreAtomic()
    {
        var profile = new ModProfile { Mods = [new ProfileMod("A.mod"), new ProfileMod("B.mod")] };
        ProfileEditor.SetEnabled(profile, [1, 1], false);
        Assert.True(profile.Mods[0].Enabled);
        Assert.False(profile.Mods[1].Enabled);
        Assert.Throws<ArgumentOutOfRangeException>(() => ProfileEditor.SetEnabled(profile, [0, 2], false));
        Assert.True(profile.Mods[0].Enabled);
        Assert.Throws<ArgumentOutOfRangeException>(() => ProfileEditor.Move(profile, [-1], 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ProfileEditor.Move(profile, [0], 3));
        Assert.Equal("A.mod", profile.Mods[0].FileName);
        ProfileEditor.Move(profile, [], 2);
        ProfileEditor.SetEnabled(profile, [0, 1], true);
        Assert.All(profile.Mods, m => Assert.True(m.Enabled));
    }

    [Theory]
    [InlineData(0, "B,D,A,C,E")]
    [InlineData(3, "A,C,B,D,E")]
    [InlineData(5, "A,C,E,B,D")]
    public void MoveUsesOriginalInsertionIndexAndStableSelection(int destination, string expected)
    {
        var profile = new ModProfile { Mods = "ABCDE".Select(c => new ProfileMod($"{c}.mod")).ToList() };
        ProfileEditor.Move(profile, [3, 1, 3], destination);
        Assert.Equal(expected.Split(',').Select(n => n + ".mod"), profile.Mods.Select(m => m.FileName));
    }
}
