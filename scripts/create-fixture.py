"""Create clearly synthetic Kenshi fixture files for desktop write/apply QA."""
from pathlib import Path
import hashlib
import json
from xml.sax.saxutils import escape

project = Path(__file__).resolve().parents[1]
base = project / '.hermes' / 'test-data' / 'desktop-fixture'
game = base / 'SteamLibrary' / 'steamapps' / 'common' / 'Kenshi'
workshop = base / 'SteamLibrary' / 'steamapps' / 'workshop' / 'content' / '233860'
names = ['Fixture UI.mod', 'Fixture World.mod', 'Fixture Animation.mod', 'Fixture Patch.mod', 'Fixture Local.mod', '日本語 Fixture.mod']
for index, name in enumerate(names):
    folder = game / 'mods' / Path(name).stem if index == 4 else workshop / str(9000000000 + index)
    folder.mkdir(parents=True, exist_ok=True)
    (folder / name).write_bytes(b'')  # Synthetic metadata-only fixture, not a playable mod.
    workshop_id = '' if index == 4 else str(9000000000 + index)
    (folder / (Path(name).stem + '.info')).write_text('<Data><mod>' + escape(name) + '</mod><title>' + escape(Path(name).stem) + '</title><publishedfileid>' + workshop_id + '</publishedfileid><tags>Test fixture</tags></Data>', encoding='utf-8')
(game / 'data').mkdir(parents=True, exist_ok=True)
cfg = game / 'data' / 'mods.cfg'
if not cfg.exists():
    cfg.write_bytes(('\r\n'.join(names[:4]) + '\r\n').encode('utf-8'))
(base / 'author-load-order.txt').write_bytes(('\r\n'.join([names[1], names[0], names[3], names[4]]) + '\r\n').encode('utf-8'))
summary = {'purpose':'Synthetic fixture for desktop QA; no real Kenshi mod data copied', 'game_path':str(game), 'workshop_path':str(workshop), 'data_dir':str(base/'manager-data'), 'cfg_sha256':hashlib.sha256(cfg.read_bytes()).hexdigest(), 'cfg_names':cfg.read_text(encoding='utf-8').splitlines(), 'installed_fixture_count':len(names)}
(base / 'fixture.json').write_text(json.dumps(summary, indent=2), encoding='utf-8')
print(json.dumps(summary, indent=2))
