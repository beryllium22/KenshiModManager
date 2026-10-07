namespace KenshiModManager.Core;

public enum ModSource { Local, Workshop }
public sealed record InstalledMod(string Id, string FileName, string FullPath, ModSource Source,
    string DisplayName, string? WorkshopId = null, IReadOnlyList<string>? Tags = null);
public sealed record ProfileMod(string FileName, bool Enabled = true, string? InstalledId = null);
public sealed class ModProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "Profile 1";
    public List<ProfileMod> Mods { get; set; } = [];
    public string? SourceUrl { get; set; }
    public string? Notes { get; set; }
}
public sealed class ManagerState
{
    public int SchemaVersion { get; set; } = 1;
    public string? GamePath { get; set; }
    public List<string> WorkshopPaths { get; set; } = [];
    public List<ModProfile> Profiles { get; set; } = [];
    public string? SelectedProfileId { get; set; }
    public string? AppliedProfileId { get; set; }
}
