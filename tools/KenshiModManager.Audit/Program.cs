using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KenshiModManager.Core;

if(args.Length < 2) throw new ArgumentException("Usage: Audit GAME_PATH OUTPUT_JSON [WORKSHOP_ROOT]");
var game = Path.GetFullPath(args[0]);
var output = Path.GetFullPath(args[1]);
var cfg = Path.Combine(game,"data","mods.cfg");
if (string.Equals(output, Path.GetFullPath(cfg), StringComparison.OrdinalIgnoreCase))
    throw new ArgumentException("OUTPUT_JSON must not be the game's data/mods.cfg; choose a fresh external report path.");
if (File.Exists(output) || Directory.Exists(output))
    throw new IOException("OUTPUT_JSON already exists; choose a fresh external report path. Existing files and aliases are never overwritten.");
var before = File.ReadAllBytes(cfg);
var names = LoadOrderCodec.Parse(new UTF8Encoding(false,true).GetString(before));
IReadOnlyList<string> workshop = args.Length > 2 ? new[]{args[2]} : SteamLibraryDiscovery.WorkshopPathsForGame(game);
var scanner = new ModInventoryScanner();
var first = Stopwatch.StartNew();
var inventory = await scanner.ScanAsync(game,workshop);
first.Stop();
var repeated = Stopwatch.StartNew();
var again = await scanner.ScanAsync(game,workshop);
repeated.Stop();
if(inventory.Count != again.Count) throw new InvalidOperationException("Inventory changed across two scans; repeat audit once downloads finish.");
var byName = inventory.ToLookup(m=>m.FileName,StringComparer.OrdinalIgnoreCase);
var missing = names.Where(n=>!byName[n].Any()).ToArray();
var ambiguous = inventory.GroupBy(m=>m.FileName,StringComparer.OrdinalIgnoreCase).Where(g=>g.Count()>1).Select(g=>g.Key).ToArray();
if(!before.AsSpan().SequenceEqual(File.ReadAllBytes(cfg))) throw new InvalidOperationException("Real cfg changed during the read-only audit; audit never wrote it.");
var scratch = Path.Combine(Path.GetDirectoryName(output)!, "test-data", "audit-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(scratch);
try
{
    // Persist/reload independent profiles in an explicitly synthetic fixture.
    var alphaPath = Path.Combine(scratch,"FixtureAlpha.mod"); File.WriteAllBytes(alphaPath,[]);
    var betaPath = Path.Combine(scratch,"FixtureBeta.mod"); File.WriteAllBytes(betaPath,[]);
    var one = ProfileStore.Create([],"",["FixtureAlpha.mod","FixtureBeta.mod"]);
    var two = ProfileStore.Duplicate([one],one,"Second Collection");
    ProfileEditor.Move(two,[1],0); ProfileEditor.SetEnabled(two,[1],false);
    if(one.Mods[0].FileName!="FixtureAlpha.mod" || !one.Mods[0].Enabled) throw new InvalidOperationException("Duplicate mutated original profile.");
    var state = new ManagerState { Profiles=[one,two],SelectedProfileId=two.Id };
    var store = new ProfileStore(Path.Combine(scratch,"state.json")); store.Save(state);
    var loaded = new ProfileStore(Path.Combine(scratch,"state.json")).Load();
    if(loaded.Profiles.Count!=2 || loaded.Profiles[1].Mods[0].FileName!="FixtureBeta.mod" || loaded.Profiles[1].Mods[1].Enabled) throw new InvalidOperationException("Profile persistence failed.");
    var shared = ProfileStore.ExportProfile(two);
    if(shared.Contains(scratch,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Shared profile contains absolute fixture path.");
    var imported = ProfileStore.ImportProfile(shared,loaded.Profiles);
    if(imported.Id==two.Id || !imported.Mods.SequenceEqual(two.Mods)) throw new InvalidOperationException("Portable profile roundtrip failed.");
    var fixtureCfg = Path.Combine(scratch,"mods.cfg"); byte[] original = [0xEF,0xBB,0xBF,..Encoding.UTF8.GetBytes("FixtureAlpha.mod\n")]; File.WriteAllBytes(fixtureCfg,original);
    InstalledMod[] fixtureInventory=[new("local:Alpha","FixtureAlpha.mod",alphaPath,ModSource.Local,"Alpha"),new("local:Beta","FixtureBeta.mod",betaPath,ModSource.Local,"Beta")];
    var writer = new SafeLoadOrderWriter(); var applied=writer.Apply(fixtureCfg,two,fixtureInventory,writer.Read(fixtureCfg),Path.Combine(scratch,"backups"));
    if(File.ReadAllText(fixtureCfg)!="FixtureBeta.mod\r\n" || !File.ReadAllBytes(applied.BackupPath).AsSpan().SequenceEqual(original)) throw new InvalidOperationException("Fixture apply/backup failed.");
    var restored=writer.Restore(fixtureCfg,applied.BackupPath,writer.Read(fixtureCfg),Path.Combine(scratch,"backups"));
    if(!File.ReadAllBytes(fixtureCfg).AsSpan().SequenceEqual(original)) throw new InvalidOperationException("Fixture exact-byte restore failed.");
    var report=new {
        game_path=game, workshop_paths=workshop, load_order_entries=names.Count, installed_mod_files=inventory.Count,
        workshop_files=inventory.Count(m=>m.Source==ModSource.Workshop),local_files=inventory.Count(m=>m.Source==ModSource.Local),
        missing_cfg_names=missing, ambiguous_names=ambiguous,
        titles_from_metadata=inventory.Count(m=>m.DisplayName!=m.FileName),
        first_process_scan_ms=first.Elapsed.TotalMilliseconds,repeated_scan_ms=repeated.Elapsed.TotalMilliseconds,
        timing_note="First/repeated process scans, not controlled cold-cache startup or UI measurements.",
        real_cfg_sha256=Convert.ToHexString(SHA256.HashData(before)),real_cfg_unchanged=true,
        profile_save_reload=true,profile_independence=true,portable_json_roundtrip=true,
        fixture_apply_verified=true,fixture_restore_exact_bytes=true,fixture_apply_sha256=applied.Sha256,fixture_restore_sha256=restored.Sha256
    };
    var json=JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true});
    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    // CreateNew closes the preflight/write race without following an existing file alias.
    using (var reportFile = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None))
    using (var reportWriter = new StreamWriter(reportFile, Encoding.UTF8))
        reportWriter.Write(json);
    Console.WriteLine(json);
}
finally { Directory.Delete(scratch,true); }
