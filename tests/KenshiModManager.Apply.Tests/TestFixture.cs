using System.Reflection;
namespace KenshiModManager.Apply.Tests;
internal static class TestFixture
{
    public static string CreateRoot(string kind)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null && !File.Exists(Path.Combine(directory.FullName,"Directory.Build.props"))) directory = directory.Parent;
        if(directory is null) throw new InvalidOperationException("Tests must run from a built checkout; repository root was not found.");
        return Path.Combine(directory.FullName,".hermes","test-data",kind+"-"+Guid.NewGuid().ToString("N"));
    }
}
