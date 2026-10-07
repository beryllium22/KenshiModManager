using KenshiModManager.Core;
namespace KenshiModManager.App;
public sealed class GameWriteService {
 private readonly bool readOnly;
 private readonly string cfgPath;
 private readonly string backupDirectory;
 private readonly SafeLoadOrderWriter writer = new();
 public GameWriteService(string gamePath,string dataDirectory,bool readOnly) {
  this.readOnly = readOnly;
  cfgPath = Path.Combine(Path.GetFullPath(gamePath), "data", "mods.cfg");
  backupDirectory = Path.Combine(Path.GetFullPath(dataDirectory), "backups");
 }
 private void EnsureWritable() { if(readOnly) throw new InvalidOperationException("Read-only mode: applying/restoring the real game load order is disabled."); }
 public LoadOrderSnapshot Read() => writer.Read(cfgPath);
 public ApplyResult Apply(ModProfile profile,IReadOnlyList<InstalledMod> inventory,LoadOrderSnapshot expected,bool? gameRunning=null) {
  EnsureWritable(); return writer.Apply(cfgPath,profile,inventory,expected,backupDirectory,gameRunning ?? IsKenshiRunning());
 }
 private static bool IsKenshiRunning() {
  foreach (var name in new[] { "kenshi_x64", "kenshi_x642", "kenshi", "forgotten construction set" }) {
   var processes = System.Diagnostics.Process.GetProcessesByName(name);
   try { if(processes.Length > 0) return true; }
   finally { foreach(var process in processes) process.Dispose(); }
  }
  return false;
 }
 public ApplyResult Restore(string backupPath,LoadOrderSnapshot expected,bool? gameRunning=null) {
  EnsureWritable(); return writer.Restore(cfgPath,backupPath,expected,backupDirectory,gameRunning ?? IsKenshiRunning());
 }
}
