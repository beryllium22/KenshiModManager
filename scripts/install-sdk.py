"""Install a verified project-local .NET SDK; leave machine SDKs untouched."""
import hashlib
import json
import os
from pathlib import Path
import subprocess
import urllib.request
import zipfile

root = Path(__file__).resolve().parents[1]
target = root / '.tools' / 'dotnet'
if (target / 'dotnet.exe').exists():
    subprocess.run([str(target / 'dotnet.exe'), '--version'], check=True)
    raise SystemExit(0)
with urllib.request.urlopen('https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json', timeout=60) as response:
    metadata = json.load(response)
sdk = metadata['releases'][0]['sdk']
artifact = next(f for f in sdk['files'] if f['rid'] == 'win-x64' and f['name'].endswith('.zip'))
cache = Path(os.environ.get('TMPDIR', str(root / '.tools' / 'downloads')))
cache.mkdir(parents=True, exist_ok=True)
archive = cache / ('kenshi-dotnet-' + sdk['version'] + '.zip')
print('Downloading SDK', sdk['version'], artifact['url'], flush=True)
with urllib.request.urlopen(artifact['url'], timeout=120) as response, archive.open('wb') as output:
    while chunk := response.read(1024 * 1024):
        output.write(chunk)
with archive.open('rb') as data:
    digest = hashlib.file_digest(data, 'sha512').hexdigest()
if digest.lower() != artifact['hash'].lower():
    raise RuntimeError('SDK SHA-512 mismatch; refusing extraction')
print('SHA-512 verified. Extracting to', target, flush=True)
target.mkdir(parents=True, exist_ok=True)
with zipfile.ZipFile(archive) as contents:
    contents.extractall(target)
subprocess.run([str(target / 'dotnet.exe'), '--version'], check=True)
(root / '.hermes').mkdir(exist_ok=True)
(root / '.hermes' / 'sdk.json').write_text(json.dumps({'version':sdk['version'], 'url':artifact['url'], 'sha512':digest}, indent=2), encoding='utf-8')
print('SDK installed without machine-wide changes.', flush=True)
