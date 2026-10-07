using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
namespace KenshiModManager.Core;
public sealed class ProfileStore
{
    private readonly string stateFilePath;
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static JsonSerializerOptions CreateJsonOptions()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(info =>
        {
            string[] required = info.Type == typeof(ManagerState) ? ["schemaVersion", "workshopPaths", "profiles"]
                : info.Type == typeof(ModProfile) ? ["id", "name", "mods"]
                : info.Type == typeof(ProfileMod) ? ["fileName", "enabled"]
                : info.Type == typeof(SharedDocument) ? ["schemaVersion", "profile"]
                : info.Type == typeof(SharedProfile) ? ["name", "mods"] : [];
            foreach (var property in info.Properties)
                if (required.Contains(property.Name, StringComparer.Ordinal)) property.IsRequired = true;
        });
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            AllowDuplicateProperties = false,
            TypeInfoResolver = resolver
        };
    }
    public ProfileStore(string stateFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateFilePath);
        this.stateFilePath = Path.GetFullPath(stateFilePath);
    }
    public ManagerState Load()
    {
        if (!File.Exists(stateFilePath)) return new ManagerState();
        try
        {
            var state = JsonSerializer.Deserialize<ManagerState>(File.ReadAllText(stateFilePath), JsonOptions)
                ?? throw new InvalidDataException("The document must contain a manager state object.");
            ValidateState(state);
            return state;
        }
        catch (Exception error) when (error is JsonException or InvalidDataException)
        {
            throw new InvalidDataException($"Cannot load state file '{stateFilePath}': {error.Message} Preserve the file and restore a backup, or explicitly move it aside before creating new state.", error);
        }
    }
    public void Save(ManagerState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        ValidateState(state);
        // Never replace unreadable user data, even when the caller has a valid new state.
        if (File.Exists(stateFilePath)) _ = Load();
        Directory.CreateDirectory(Path.GetDirectoryName(stateFilePath)!);
        var staging = stateFilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(state, JsonOptions));
            using (var stream = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(true);
            }
            if (File.Exists(stateFilePath)) File.Replace(staging, stateFilePath, null);
            else File.Move(staging, stateFilePath);
        }
        finally { if (File.Exists(staging)) File.Delete(staging); }
    }
    // Sharing deliberately has no state/profile IDs or machine installation paths.
    private sealed class SharedDocument
    {
        public int SchemaVersion { get; set; }
        public SharedProfile? Profile { get; set; }
    }
    private sealed class SharedProfile
    {
        public string? Name { get; set; }
        public List<ProfileMod>? Mods { get; set; }
        public string? SourceUrl { get; set; }
        public string? Notes { get; set; }
    }

    public static string ExportProfile(ModProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ValidateProfile(profile);
        ValidatePortableMetadata(profile, exporting: true);
        return JsonSerializer.Serialize(new SharedDocument
        {
            SchemaVersion = 1,
            Profile = new SharedProfile
            {
                Name = profile.Name,
                Mods = profile.Mods.Select(m => new ProfileMod(m.FileName, m.Enabled,
                    m.InstalledId?.StartsWith("workshop:", StringComparison.Ordinal) == true ? m.InstalledId : null)).ToList(),
                SourceUrl = profile.SourceUrl,
                Notes = profile.Notes
            }
        }, JsonOptions);
    }

    public static ModProfile ImportProfile(string json, IEnumerable<ModProfile> existing)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(existing);
        try
        {
            var document = JsonSerializer.Deserialize<SharedDocument>(json, JsonOptions)
                ?? throw new InvalidDataException("The shared document must be an object.");
            if (document.SchemaVersion != 1)
                throw new InvalidDataException($"Unsupported shared schema version {document.SchemaVersion}; only version 1 is supported.");
            var shared = document.Profile ?? throw new InvalidDataException("The document needs a profile object.");
            var validated = new ModProfile { Name = shared.Name!, Mods = shared.Mods!, SourceUrl = shared.SourceUrl, Notes = shared.Notes };
            ValidateProfile(validated);
            ValidatePortableMetadata(validated, exporting: false);
            var profile = Create(existing, shared.Name, []);
            profile.Mods = validated.Mods.Select(m => new ProfileMod(m.FileName, m.Enabled, m.InstalledId)).ToList();
            profile.SourceUrl = shared.SourceUrl;
            profile.Notes = shared.Notes;
            return profile;
        }
        catch (Exception error) when (error is JsonException or InvalidDataException)
        {
            throw new InvalidDataException($"Cannot import profile: {error.Message} Choose a valid version 1 portable profile JSON export.", error);
        }
    }

    private static void ValidatePortableMetadata(ModProfile profile, bool exporting)
    {
        if (!string.IsNullOrEmpty(profile.SourceUrl) &&
            (!Uri.TryCreate(profile.SourceUrl, UriKind.Absolute, out var url) ||
             (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps) || string.IsNullOrEmpty(url.Host)))
            throw new InvalidDataException("Shared profile source URL must be an HTTP(S) web link, not a machine-local path.");
        foreach (var mod in profile.Mods)
        {
            var reference = mod.InstalledId;
            if (reference is null) continue;
            // Local scanner IDs and legacy absolute IDs are omitted on export.
            if (exporting && !reference.StartsWith("workshop:", StringComparison.Ordinal)) continue;
            if (!reference.StartsWith("workshop:", StringComparison.Ordinal))
                throw new InvalidDataException("Shared profile references must use workshop:<numeric ID>/<exact filename>, never local paths.");
            var split = reference.IndexOf('/', 9);
            if (split < 10 || !reference.AsSpan(9, split - 9).ToArray().All(c => c is >= '0' and <= '9') ||
                !string.Equals(reference[(split + 1)..], mod.FileName, StringComparison.Ordinal))
                throw new InvalidDataException($"Invalid Workshop reference '{reference}'; use workshop:<numeric ID>/{mod.FileName}.");
        }
    }

    private static void ValidateState(ManagerState state)
    {
        if (state.SchemaVersion != 1)
            throw new InvalidDataException($"Unsupported state schema version {state.SchemaVersion}; this application supports version 1. Use a compatible application or restore a backup.");
        if (state.Profiles is null || state.WorkshopPaths is null || state.WorkshopPaths.Any(p => p is null))
            throw new InvalidDataException("Profiles and Workshop paths must be non-null lists without null entries.");
        var ids = new HashSet<Guid>();
        foreach (var profile in state.Profiles)
        {
            ValidateProfile(profile);
            if (!ids.Add(Guid.Parse(profile.Id)))
                throw new InvalidDataException($"Duplicate profile ID '{profile.Id}'. Each profile needs a unique ID.");
        }
        foreach (var current in new[] { state.SelectedProfileId, state.AppliedProfileId })
            if (current is not null && (!Guid.TryParseExact(current, "D", out var id) || !ids.Contains(id)))
                throw new InvalidDataException($"Selected/applied profile ID '{current}' does not refer to an existing profile.");
    }

    private static void ValidateProfile(ModProfile? profile)
    {
        if (profile is null) throw new InvalidDataException("Profiles must not contain null entries.");
        if (!Guid.TryParseExact(profile.Id, "D", out _)) throw new InvalidDataException("Profile IDs must be GUID strings in dashed format.");
        if (string.IsNullOrWhiteSpace(profile.Name) || profile.Name.Any(char.IsControl))
            throw new InvalidDataException("Profile names must be non-empty and contain no control characters.");
        if (profile.Mods is null) throw new InvalidDataException($"Profile '{profile.Name}' needs a non-null mod list.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mod in profile.Mods)
        {
            if (mod is null) throw new InvalidDataException($"Profile '{profile.Name}' contains a null mod entry.");
            try { LoadOrderCodec.ValidateFileName(mod.FileName); }
            catch (FormatException error) { throw new InvalidDataException($"Profile '{profile.Name}' has invalid filename '{mod.FileName}': {error.Message}", error); }
            if (!names.Add(mod.FileName)) throw new InvalidDataException($"Profile '{profile.Name}' contains duplicate mod filename '{mod.FileName}' (case-insensitive).");
        }
    }

    public static ModProfile Duplicate(IEnumerable<ModProfile> existing, ModProfile original, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(original);
        ValidateProfile(original);
        var copy = Create(existing, name, []);
        copy.Mods = original.Mods.Select(m => new ProfileMod(m.FileName, m.Enabled, m.InstalledId)).ToList();
        copy.SourceUrl = original.SourceUrl;
        copy.Notes = original.Notes;
        return copy;
    }
    public static ModProfile Create(IEnumerable<ModProfile> existing, string? name, IEnumerable<string> enabledNames)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(enabledNames);
        var profiles = existing.ToArray();
        var actualName = name?.Trim();
        if (string.IsNullOrEmpty(actualName))
        {
            var number = 1;
            while (profiles.Any(p => string.Equals(p.Name, $"Profile {number}", StringComparison.OrdinalIgnoreCase))) number++;
            actualName = $"Profile {number}";
        }
        var mods = enabledNames.Select(n => { LoadOrderCodec.ValidateFileName(n); return new ProfileMod(n); }).ToList();
        var profile = new ModProfile { Name = actualName, Mods = mods };
        // IDs are identities; duplicate display names are intentionally allowed.
        while (profiles.Any(p => string.Equals(p.Id, profile.Id, StringComparison.OrdinalIgnoreCase)))
            profile.Id = Guid.NewGuid().ToString();
        ValidateProfile(profile);
        return profile;
    }
}
