namespace KenshiModManager.Core;
public static class ProfileEditor
{
    public static void SetEnabled(ModProfile profile, IEnumerable<int> selectedIndices, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(selectedIndices);
        var indices = selectedIndices.Distinct().ToArray();
        if (indices.Any(i => i < 0 || i >= profile.Mods.Count)) throw new ArgumentOutOfRangeException(nameof(selectedIndices));
        foreach (var index in indices) profile.Mods[index] = profile.Mods[index] with { Enabled = enabled };
    }
    public static void Move(ModProfile profile, IEnumerable<int> selectedIndices, int destinationIndex)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(selectedIndices);
        if (destinationIndex < 0 || destinationIndex > profile.Mods.Count) throw new ArgumentOutOfRangeException(nameof(destinationIndex));
        var indices = selectedIndices.Distinct().Order().ToArray();
        if (indices.Any(i => i < 0 || i >= profile.Mods.Count)) throw new ArgumentOutOfRangeException(nameof(selectedIndices));
        var moved = indices.Select(i => profile.Mods[i]).ToList();
        var insertion = destinationIndex - indices.Count(i => i < destinationIndex);
        foreach (var index in indices.Reverse()) profile.Mods.RemoveAt(index);
        profile.Mods.InsertRange(insertion, moved);
    }
}
