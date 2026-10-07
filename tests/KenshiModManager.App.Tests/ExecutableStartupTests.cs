using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using KenshiModManager.Core;
using Xunit;
namespace KenshiModManager.App.Tests;
public class ExecutableStartupTests {
 [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr window,int attribute,out int value,int size);
 [Fact] public async Task RealExeStartsWithFixtureFlagsSavesStateAndClosesCleanly() {
  var repo=new DirectoryInfo(AppContext.BaseDirectory); while(repo is not null && !File.Exists(Path.Combine(repo.FullName,"Directory.Build.props"))) repo=repo.Parent;
  Assert.NotNull(repo);
  var fixture=Path.Combine(repo.FullName,".hermes","test-data","exe-"+Guid.NewGuid().ToString("N")); var game=Path.Combine(fixture,"game"); var data=Path.Combine(fixture,"manager"); var workshop=Path.Combine(fixture,"workshop");
  Directory.CreateDirectory(Path.Combine(game,"data")); Directory.CreateDirectory(Path.Combine(game,"mods","example")); Directory.CreateDirectory(workshop);
  var cfg=Path.Combine(game,"data","mods.cfg"); var original=System.Text.Encoding.UTF8.GetBytes("Example.mod\r\n"); await File.WriteAllBytesAsync(cfg,original); await File.WriteAllTextAsync(Path.Combine(game,"mods","example","Example.mod"),"");
  var store=new ProfileStore(Path.Combine(data,"state.json")); store.Save(new ManagerState {WorkshopPaths=[workshop]});
  var exe=Path.Combine(AppContext.BaseDirectory,"KenshiModManager.App.exe"); Assert.True(File.Exists(exe),"WinExe apphost was not produced");
  var start=new ProcessStartInfo(exe) {UseShellExecute=false,WorkingDirectory=fixture}; start.ArgumentList.Add("--game-path"); start.ArgumentList.Add(game); start.ArgumentList.Add("--data-dir"); start.ArgumentList.Add(data); start.ArgumentList.Add("--read-only");
  start.Environment["DOTNET_ROOT"]=Path.GetFullPath(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(),"..","..",".."));
  using var process=Process.Start(start) ?? throw new InvalidOperationException("The apphost could not start");
  try {
   var watch=Stopwatch.StartNew(); ManagerState saved;
   do {await Task.Delay(50); Assert.False(process.HasExited,"Application exited before state became visible"); if(watch.Elapsed>TimeSpan.FromSeconds(15)) throw new TimeoutException("Executable failed to load/save fixture state"); saved=store.Load();} while(saved.Profiles.Count==0);
   Assert.Equal("Current Kenshi Setup",Assert.Single(saved.Profiles).Name); Assert.Null(saved.AppliedProfileId); Assert.Equal(original,await File.ReadAllBytesAsync(cfg));
   process.Refresh(); Assert.NotEqual(IntPtr.Zero,process.MainWindowHandle); Assert.Equal(0,DwmGetWindowAttribute(process.MainWindowHandle,20,out var darkCaption,sizeof(int))); Assert.Equal(1,darkCaption); Assert.Contains("Kenshi Mod Manager",process.MainWindowTitle); Assert.True(process.CloseMainWindow()); await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); Assert.Equal(0,process.ExitCode);
   Assert.Equal(original,await File.ReadAllBytesAsync(cfg));
  } finally {if(!process.HasExited) {process.Kill(true); await process.WaitForExitAsync();}}
 }
}
