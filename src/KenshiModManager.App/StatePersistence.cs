using System.Text.Json;
using System.IO;
using KenshiModManager.Core;
namespace KenshiModManager.App;
/// <summary>Only writes manager-owned state; immutable captures keep background saves race-free.</summary>
public sealed class StatePersistence {
 private readonly ProfileStore store;
 private readonly SemaphoreSlim gate=new(1,1);
 public string StatePath { get; }
 public StatePersistence(string dataDirectory) { StatePath=Path.Combine(Path.GetFullPath(dataDirectory),"state.json"); store=new(StatePath); }
 public ManagerState Load() => store.Load();
 public static ManagerState Snapshot(ManagerState state) => JsonSerializer.Deserialize<ManagerState>(JsonSerializer.Serialize(state))!;
 public async Task SaveAsync(ManagerState captured) {
  await gate.WaitAsync();
  try { await Task.Run(()=>store.Save(captured)); }
  finally { gate.Release(); }
 }
}
