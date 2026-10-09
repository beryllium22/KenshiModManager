"""Build a self-contained Windows x64 package at the declared project version."""
from pathlib import Path
import argparse
import hashlib
import json
import re
import shutil
import subprocess
import tempfile
import urllib.request
import zipfile
import xml.etree.ElementTree as ET


def refuse_existing(targets):
    for target in targets:
        if target.exists():
            raise SystemExit(f'Output already exists: {target}. Choose a new --label; existing packages are preserved.')


def included_framework_versions(runtimeconfig):
    """Read the exact self-contained framework versions; never guess from the SDK."""
    try:
        configuration = json.loads(runtimeconfig.read_text(encoding='utf-8'))
        entries = configuration['runtimeOptions']['includedFrameworks']
        if not isinstance(entries, list) or not entries:
            raise ValueError('includedFrameworks must be a nonempty list')
        frameworks = {}
        version_pattern = r'(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?'
        for entry in entries:
            name, version = entry['name'], entry['version']
            if not isinstance(name, str) or not name or name in frameworks:
                raise ValueError(f'Missing, invalid or duplicate framework name: {name!r}')
            if not isinstance(version, str) or not re.fullmatch(version_pattern, version):
                raise ValueError(f'Invalid framework version for {name}: {version!r}')
            frameworks[name] = version
        for name in ('Microsoft.NETCore.App', 'Microsoft.WindowsDesktop.App'):
            if name not in frameworks:
                raise ValueError(f'Missing framework: {name}')
        return frameworks
    except (OSError, ValueError, KeyError, TypeError) as exc:
        raise RuntimeError(f'Cannot resolve includedFrameworks from {runtimeconfig}: {exc}. '
            'Re-publish the self-contained app with valid includedFrameworks name/version entries; '
            'no fallback runtime or notice version is used.') from exc


def main():
    project = Path(__file__).resolve().parents[1]
    version = ET.parse(project / 'Directory.Build.props').findtext('./PropertyGroup/Version')
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dotnet', help='Explicit .NET SDK executable')
    parser.add_argument('--label', default='V' + version, help='Output label only, e.g. dev-finish; not an app version bump')
    args = parser.parse_args()
    if len(args.label) > 80 or not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9_-]*(?:\.[A-Za-z0-9_-]+)*', args.label):
        raise SystemExit('Invalid --label: use 1-80 ASCII letters/digits, hyphens, underscores or interior dots; '
            'no paths, spaces or trailing dots. Example: --label dev-finish-20261007')
    local_sdk = project / '.tools' / 'dotnet' / 'dotnet.exe'
    dotnet = args.dotnet or (str(local_sdk) if local_sdk.exists() else shutil.which('dotnet'))
    if not dotnet:
        raise SystemExit('Install .NET 10 SDK or run python scripts/install-sdk.py first.')
    app = project / 'src' / 'KenshiModManager.App' / 'KenshiModManager.App.csproj'
    artifacts = project / 'artifacts'
    out = artifacts / ('KenshiModManager-' + args.label + '-win-x64')
    archive = artifacts / (out.name + '.zip')
    checksums = artifacts / ('SHA256SUMS-' + args.label + '.txt')
    evidence = project / '.hermes'
    manifest = evidence / ('package-' + args.label + '.json')
    targets = (out, archive, checksums, manifest)
    refuse_existing(targets)
    artifacts.mkdir(exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='package-stage-', dir=artifacts) as temporary:
        staging = Path(temporary)
        published = staging / out.name
        subprocess.run([dotnet, 'publish', str(app), '-c', 'Release', '-r', 'win-x64',
            '--self-contained', 'true', '-p:PublishSingleFile=false', '-p:PublishTrimmed=false',
            '-p:DebugType=None', '-p:DebugSymbols=false', '-o', str(published), '--nologo'],
            cwd=project, check=True)
        if not (published / 'KenshiModManager.App.exe').is_file():
            raise RuntimeError('Expected application executable missing')
        for name in ('README.md', 'LICENSE', 'THIRD-PARTY-NOTICES.md', 'CHANGELOG.md'):
            shutil.copyfile(project / name, published / name)
        runtimeconfig = published / 'KenshiModManager.App.runtimeconfig.json'
        frameworks = included_framework_versions(runtimeconfig)
        runtime_version = frameworks['Microsoft.NETCore.App']
        desktop_version = frameworks['Microsoft.WindowsDesktop.App']
        notices = {
            'DOTNET-LICENSE.txt': f'https://raw.githubusercontent.com/dotnet/runtime/v{runtime_version}/LICENSE.TXT',
            'DOTNET-THIRD-PARTY-NOTICES.txt': f'https://raw.githubusercontent.com/dotnet/runtime/v{runtime_version}/THIRD-PARTY-NOTICES.TXT',
            'WPF-LICENSE.txt': f'https://raw.githubusercontent.com/dotnet/wpf/v{desktop_version}/LICENSE.TXT',
        }
        for name, url in notices.items():
            with urllib.request.urlopen(url, timeout=60) as response:
                data = response.read()
            if not data or data.startswith(b'<'):
                raise RuntimeError('Invalid upstream notice: ' + name)
            (published / name).write_bytes(data)
        staged_archive = staging / archive.name
        with zipfile.ZipFile(staged_archive, 'x', zipfile.ZIP_DEFLATED, compresslevel=6) as zipped:
            for file in sorted(published.rglob('*')):
                if file.is_file() and file.suffix.lower() != '.pdb':
                    zipped.write(file, (Path(out.name) / file.relative_to(published)).as_posix())
        digest = hashlib.sha256(staged_archive.read_bytes()).hexdigest()
        summary = {'version': 'V' + version, 'label': args.label,
            'runtime_frameworks': frameworks, 'notice_urls': notices,
            'executable': str(out / 'KenshiModManager.App.exe'), 'archive': str(archive),
            'archive_bytes': staged_archive.stat().st_size, 'archive_sha256': digest,
            'checksums': str(checksums), 'packaging': 'Self-contained .NET 10 Windows x64; extract entire folder'}
        refuse_existing(targets)
        published.rename(out)
        with staged_archive.open('rb') as source, archive.open('xb') as destination:
            shutil.copyfileobj(source, destination)
        with checksums.open('x', encoding='utf-8', newline='\n') as destination:
            destination.write(digest + '  ' + archive.name + '\n')
        evidence.mkdir(exist_ok=True)
        with manifest.open('x', encoding='utf-8') as destination:
            json.dump(summary, destination, indent=2)
    print(json.dumps(summary, indent=2))


if __name__ == '__main__':
    main()
