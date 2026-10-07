# Kenshi Mod Manager — V0.5

A native Windows **Profile Studio** for Kenshi: a focused profile editor, searchable installed-mod picker, and deliberately separate game-write controls. Steam continues to own Workshop subscriptions and downloads.

## Download

Download **KenshiModManager-V0.5-win-x64.zip** from [Releases](https://github.com/beryllium22/KenshiModManager/releases/tag/V0.5). Extract the **entire folder**, then run `KenshiModManager.App.exe`. The Windows x64 package includes .NET; a separate .NET installation is not required.

This is an early, unsigned release—not a signed installer. Windows may warn about unsigned software. Administrator privileges and Steam credentials are not required by the manager; your directory permissions still control whether the game configuration can be written.

## Profile Studio

1. Choose your Kenshi installation. The manager discovers local mods, installed Steam Workshop mods, and the current `data/mods.cfg` order.
2. Keep **Current Kenshi Setup**, create an **empty profile**, or explicitly copy an existing profile.
3. Use **Add installed mods** to search the inventory and add selected filenames enabled or disabled. Rescanning does not silently add mods to profiles.
4. Paste or import an author's recommended order. Imports preview a **new profile** before confirmation; they never append to or overwrite the selected profile.
5. Enable, disable, reorder, and save independent profiles. Review the game order and explicitly confirm Apply when you actually want to use one.

Selecting, editing, importing, or saving a profile **never automatically applies it to Kenshi**. The selected profile, historical last-applied profile, and observed current game contents remain separate.

### Editing and sharing

- Search and filter the selected profile; inspect **Issues**, **Notes & source**, and mod details separately.
- Native Ctrl/Shift multi-selection; contextual Enable/Disable and ordering actions.
- Drag, Up/Down, Top/Bottom, and move-to-position controls preserve full-list order. Dragging under an active filter is intentionally disabled.
- Ctrl+F: search; Ctrl+Z/Ctrl+Y: undo/redo; Space: toggle selection; Alt+Up/Down: move selection.
- Plain load-order export shares enabled filenames and order. JSON export preserves the profile's enabled/disabled members, source references, notes, and order—not mod binaries or absolute installation paths.
- Game setup and exact-byte backup restoration are separate from manager Undo.

Load orders contain one **exact `.mod` filename per line**. Missing and ambiguous names remain visible. Filename spelling, disabled entries, and profile positions are not silently repaired or pruned. Removing author numbering is an explicit import option, off by default.

## Safe game writes

Apply checks missing/ambiguous enabled mods, duplicates, running-game state, and changes to `mods.cfg` since the reviewed snapshot. It creates an exact-byte backup, flushes a same-volume staged write, atomically replaces the file, and reads the result back before reporting success. Concurrent manager writers are serialized.

Restore is also explicit: it backs up the current configuration and restores the chosen backup's exact bytes, including BOM and newline formatting. A backup may reference mods no longer installed—review it before playing.

`--read-only` blocks Apply and Restore while retaining profile editing and review. If another tool or Kenshi changes the configuration, reload and review before retrying.

**Limitations:** no automatic compatibility/conflict solver, Workshop downloads or subscriptions, mod-version pinning, save swapping, mod-asset snapshots, or injected-DLL management. Steam updates may affect every profile using a mod. Screen-reader and high-DPI coverage is not claimed complete.

## Data and isolated operation

Manager state defaults to `%LOCALAPPDATA%\KenshiModManager`. Override it with `--data-dir`:

```text
KenshiModManager.App.exe --game-path "C:\Games\Kenshi" --data-dir "C:\KenshiManagerQA" --read-only
```

Use `python scripts/create-fixture.py` to generate synthetic, non-playable fixtures for Apply/Restore testing. Never test game writes against a real installation without deliberate consent.

## Build and test

Requirements: Windows x64, .NET 10 SDK, and Python 3 for optional setup/package scripts. Install .NET through Microsoft, or use `python scripts/install-sdk.py` for a SHA-512-verified project-local SDK. With `dotnet` on PATH:

```sh
dotnet build src/KenshiModManager.App/KenshiModManager.App.csproj -c Release
dotnet test tests/KenshiModManager.Core.Tests/KenshiModManager.Core.Tests.csproj -c Release
dotnet test tests/KenshiModManager.Apply.Tests/KenshiModManager.Apply.Tests.csproj -c Release
dotnet test tests/KenshiModManager.App.Tests/KenshiModManager.App.Tests.csproj -c Release
python scripts/package.py
```

See [TESTING.md](TESTING.md) for verification boundaries and [CHANGELOG.md](CHANGELOG.md) for V0.5 changes. Build output, local manager state, installed inventory, internal planning, credentials, and SDK caches are not source-controlled.

## License

Copyright (C) 2026 beryllium22. Licensed under **GNU GPL version 3 only** (`GPL-3.0-only`); see [LICENSE](LICENSE). This software is distributed **without warranty**. Third-party runtime components retain their own licenses; see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

Unofficial community software, not affiliated with or endorsed by Lo-Fi Games or Valve. Kenshi and Steam names belong to their respective owners. No game/mod binaries or third-party application assets are distributed with the source.
