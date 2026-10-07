# Testing and verification

## Canonical commands

Run from a Windows checkout with .NET 10 SDK on PATH:

```sh
dotnet test tests/KenshiModManager.Core.Tests/KenshiModManager.Core.Tests.csproj -c Release
dotnet test tests/KenshiModManager.Apply.Tests/KenshiModManager.Apply.Tests.csproj -c Release
dotnet test tests/KenshiModManager.App.Tests/KenshiModManager.App.Tests.csproj -c Release
dotnet build src/KenshiModManager.App/KenshiModManager.App.csproj -c Release
```

All three suites must pass against the final source. A subset pass, old binary, source check, or successful build alone is not release validation. Native WPF and executable smoke tests require a Windows desktop session; they are not portable Linux tests.

Tests locate the checkout through `Directory.Build.props`. Synthetic state, mods, cfg files, backups, and evidence live under the ignored `.hermes/test-data` directory. They must never write to the real game or depend on a preexisting local `.hermes` directory.

## Fixture and desktop QA

`python scripts/create-fixture.py` creates six synthetic non-playable mods and isolated manager data. Launch with `--game-path <fixture game>` and `--data-dir <fixture manager-data>` for explicit fixture Apply and Restore.

Verify empty/copy-selected creation, explicit enabled/disabled addition, rename/duplicate, filtering, multi-selection, ordering, undo/redo, portable export, persistence, and missing names. Two-stage import smoke coverage checks that preview and cancellation leave the selected profile and cfg unchanged; only confirmation creates a new profile. Verify game writes by comparing actual bytes and backups.

For real-library viewing, always supply an isolated manager-data directory and `--read-only`. Capture native wide/compact windows after visual changes. Do not interpret a screenshot or source inspection as runtime interaction coverage, screen-reader certification, or comprehensive DPI testing.

## Optional read-only inventory audit

```sh
dotnet run --project tools/KenshiModManager.Audit -c Release -- "C:/Games/Kenshi" ".hermes/audit.json" "C:/Steam/steamapps/workshop/content/233860"
```

Substitute your own existing paths. The tool reads real inventory/configuration and checks the real cfg hash. Its persistence, portable-sharing, Apply, and exact Restore checks write only synthetic files beside the specified report. Keep the report beneath `.hermes`; it contains local paths and inventory details and must not be published.

`OUTPUT_JSON` must be a **fresh report path**, not the game's `data/mods.cfg`. The audit rejects normalized cfg collisions (including relative paths and case variants) and existing output files/directories before creating scratch files or writing output. Existing symlink/hardlink outputs are rejected too. Report creation uses create-new/no-overwrite semantics, so an output that appears after the initial check cannot be overwritten. To rerun, choose another report filename.

Focused output-safety regression (synthetic fixtures only, using this checkout's SDK):

```sh
.tools/dotnet/dotnet.exe test tests/KenshiModManager.Core.Tests/KenshiModManager.Core.Tests.csproj -c Release --filter FullyQualifiedName~AuditOutputGuardTests
```

This checks byte-preserving early rejection for direct/case/relative cfg paths, existing reports, and cfg symlink/hardlink aliases, plus successful fresh external report generation. These focused tests are not full release validation.

## Packaging verification

`python scripts/package.py` publishes a separate V0.5 self-contained Windows x64 folder/ZIP, includes license notices, and records SHA-256 evidence under `.hermes`. Extract it into a new isolated location; launch the extracted executable against a synthetic fixture without relying on a machine .NET installation, verify state/cfg behavior and a clean exit, and verify version metadata before uploading.

Private planning, captured real inventory, downloaded SDKs, build/test output, authentication helpers, and release receipts remain ignored. Public source is assembled from an explicit audited allowlist.

## Performance

Scanning, background metadata loading, UI startup, and interaction latency are separate measurements. Warm filesystem scans are not cold-start benchmarks. No performance guarantee follows merely from virtualization or the choice of native WPF.
