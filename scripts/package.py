"""Build the V0.5 self-contained Windows x64 distribution; preserve older packages."""
from pathlib import Path
import argparse
import hashlib
import json
import shutil
import subprocess
import urllib.request
import zipfile

project = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--dotnet', help='Explicit .NET SDK executable')
args = parser.parse_args()
local_sdk = project / '.tools' / 'dotnet' / 'dotnet.exe'
dotnet = args.dotnet or (str(local_sdk) if local_sdk.exists() else shutil.which('dotnet'))
if not dotnet:
    raise SystemExit('Install .NET 10 SDK or run python scripts/install-sdk.py first.')
app = project / 'src' / 'KenshiModManager.App' / 'KenshiModManager.App.csproj'
out = project / 'artifacts' / 'KenshiModManager-V0.5-win-x64'
subprocess.run([dotnet, 'publish', str(app), '-c', 'Release', '-r', 'win-x64',
    '--self-contained', 'true', '-p:PublishSingleFile=false', '-p:PublishTrimmed=false',
    '-p:DebugType=None', '-p:DebugSymbols=false', '-o', str(out), '--nologo'], cwd=project, check=True)
exe = out / 'KenshiModManager.App.exe'
if not exe.exists():
    raise RuntimeError('Expected application executable missing')
for name in ('README.md', 'LICENSE', 'THIRD-PARTY-NOTICES.md', 'CHANGELOG.md'):
    shutil.copyfile(project / name, out / name)
notices = {
    'DOTNET-LICENSE.txt': 'https://raw.githubusercontent.com/dotnet/runtime/v10.0.0/LICENSE.TXT',
    'DOTNET-THIRD-PARTY-NOTICES.txt': 'https://raw.githubusercontent.com/dotnet/runtime/v10.0.0/THIRD-PARTY-NOTICES.TXT',
    'WPF-LICENSE.txt': 'https://raw.githubusercontent.com/dotnet/wpf/v10.0.0/LICENSE.TXT',
}
for name, url in notices.items():
    with urllib.request.urlopen(url, timeout=60) as response:
        data = response.read()
    if not data or data.startswith(b'<'):
        raise RuntimeError('Invalid upstream notice: ' + name)
    (out / name).write_bytes(data)
archive = project / 'artifacts' / (out.name + '.zip')
with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as zipped:
    for file in sorted(out.rglob('*')):
        if file.is_file() and file.suffix.lower() != '.pdb':
            zipped.write(file, str(Path(out.name) / file.relative_to(out)))
digest = hashlib.sha256(archive.read_bytes()).hexdigest()
checksums = project / 'artifacts' / 'SHA256SUMS-V0.5.txt'
checksums.write_text(digest + '  ' + archive.name + '\n', encoding='utf-8')
summary = {'version': 'V0.5', 'executable': str(exe), 'archive': str(archive),
    'archive_bytes': archive.stat().st_size, 'archive_sha256': digest,
    'checksums': str(checksums), 'packaging': 'Self-contained .NET 10 Windows x64; extract entire folder'}
evidence = project / '.hermes'
evidence.mkdir(exist_ok=True)
(evidence / 'package-V0.5.json').write_text(json.dumps(summary, indent=2), encoding='utf-8')
print(json.dumps(summary, indent=2))
