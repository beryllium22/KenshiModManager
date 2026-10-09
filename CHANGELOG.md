# Changelog

## V0.5.1

Patch release; profile schema and game-write behavior are unchanged.

- Fixed enabled checkboxes so UI Automation toggles update the actual profile and undo history, not only the visual checkmark.
- Kept native Space handling for focused checkboxes; the profile-wide shortcut no longer swallows it, and toggles retain keyboard focus when the grid refreshes.
- Prevented competing app instances from autosaving into the same manager data folder: one window holds the writer lease until it closes; independent data folders remain available. Read-only game mode still autosaves manager profiles.
- Hardened packaging with fresh staging, explicit output labels, no overwrite, and license notices matched to each bundled framework's actual version.
- Corrected synthetic mod metadata to the scanner's actual XML format and added generator/scanner regressions.
- Added native autosave failure/retry coverage without discarding profile edits or writing game configuration.

## V0.5

Initial public Profile Studio release.

- Native .NET 10/WPF Windows x64 application with a jade/dark focused editor and profile sidebar.
- Independent profiles with explicit installed-mod membership; genuinely empty profiles stay empty after rescans.
- Searchable installed-mod picker with enabled/disabled batch addition and full-filename tooltips.
- Two-step new-profile import preview with cancellation and literal-filename preservation.
- Contextual selection/order controls, filtering, undo/redo, issues, notes/source, and mod details.
- Portable JSON and plain enabled-order sharing; schema-1 profile retention.
- Game-match observation distinct from selected and last-applied profile identity.
- Explicit reviewed Apply and exact-byte backup Restore, snapshot change detection, atomic replacement, and read-back verification.
- Read-only review and isolated synthetic tests; self-contained, unsigned Windows x64 package.

Steam continues to manage subscriptions/downloads. No automatic mod compatibility solver or Workshop-version pinning is included.
