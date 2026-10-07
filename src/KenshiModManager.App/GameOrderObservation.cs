using System.Text;
using KenshiModManager.Core;
namespace KenshiModManager.App;

/// <summary>An observation is not a write precondition and never substitutes for writer validation.</summary>
public static class GameOrderObservation {
 public static string Describe(ModProfile? profile,LoadOrderSnapshot? snapshot,bool stale=false) {
  if(profile is null) return "No profile selected · game equality unknown";
  if(snapshot is null || stale) return "Current game order unknown · reload a readable game snapshot";
  try {
   var current=LoadOrderCodec.Parse(new UTF8Encoding(false,true).GetString(snapshot.Bytes));
   if(current.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=current.Count) return "Current game order unknown · duplicate filenames in mods.cfg";
   var expected=profile.Mods.Where(m=>m.Enabled).Select(m=>m.FileName).ToArray();
   foreach(var name in expected) LoadOrderCodec.ValidateFileName(name);
   if(expected.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=expected.Length) return "Profile has duplicate enabled filenames · game equality unknown";
   return expected.SequenceEqual(current,StringComparer.OrdinalIgnoreCase)?"Matches observed game order · no write needed":"Differs from observed game order · profile edits are not applied";
  } catch(Exception error) when(error is FormatException or DecoderFallbackException) {
   return "Current game order unknown · mods.cfg is not a valid literal load order";
  }
 }
}