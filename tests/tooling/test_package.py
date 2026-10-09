"""Packaging regressions: real filesystem/ZIP, fake publish and HTTP only."""
from contextlib import redirect_stdout
import io
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch
import zipfile


PROJECT = Path(__file__).resolve().parents[2]
SCRIPT = PROJECT / 'scripts' / 'package.py'


class PackageTests(unittest.TestCase):
    def setUp(self):
        data_root = PROJECT / '.hermes' / 'test-data'
        data_root.mkdir(parents=True, exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(prefix='package-', dir=data_root)
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        (self.root / 'artifacts').mkdir()
        (self.root / '.hermes').mkdir()
        for name in ('README.md', 'LICENSE', 'THIRD-PARTY-NOTICES.md', 'CHANGELOG.md'):
            (self.root / name).write_text('fixture ' + name, encoding='utf-8')
        (self.root / 'Directory.Build.props').write_text(
            '<Project><PropertyGroup><Version>0.5.1</Version></PropertyGroup></Project>',
            encoding='utf-8')
        self.publish_dirs = []
        self.urls = []
        self.frameworks = [
            {'name': 'Microsoft.NETCore.App', 'version': '10.0.17'},
            {'name': 'Microsoft.WindowsDesktop.App', 'version': '10.0.18'},
        ]
        self.payload = b'fresh executable'

    def publish(self, command, **kwargs):
        out = Path(command[command.index('-o') + 1])
        self.publish_dirs.append(out)
        out.mkdir(parents=True, exist_ok=True)
        (out / 'KenshiModManager.App.exe').write_bytes(self.payload)
        (out / 'KenshiModManager.App.runtimeconfig.json').write_text(json.dumps({
            'runtimeOptions': {'includedFrameworks': self.frameworks}
        }), encoding='utf-8')
        (out / 'debug.pdb').write_bytes(b'not shipped')
        (out / 'nested').mkdir(exist_ok=True)
        (out / 'nested' / 'resource.dll').write_bytes(b'resource')

    def download(self, url, **kwargs):
        self.urls.append(url)
        return io.BytesIO(('notice: ' + url).encode('utf-8'))

    def run_package(self, *args):
        output = io.StringIO()
        error = None
        namespace = {'__file__': str(self.root / 'scripts' / 'package.py'),
                     '__name__': '__main__'}
        with patch.object(sys, 'argv', ['package.py', '--dotnet', 'fake-dotnet', *args]), \
                patch('subprocess.run', side_effect=self.publish), \
                patch('urllib.request.urlopen', side_effect=self.download), \
                redirect_stdout(output):
            try:
                exec(compile(SCRIPT.read_text(encoding='utf-8'), str(SCRIPT), 'exec'), namespace)
            except (SystemExit, Exception) as exc:
                error = exc
        return error, output.getvalue()

    def test_unsafe_labels_are_refused_before_publication(self):
        for label in ('', '../escape', 'dev/../../../escape', 'dev\\escape',
                      '/absolute', 'C:stream', 'dev space', 'dev.', 'dev\n',
                      'dév', '.', '..', 'x' * 81):
            with self.subTest(label=label):
                self.publish_dirs.clear()
                error, _ = self.run_package('--label', label)
                self.assertIsInstance(error, SystemExit, str(error))
                self.assertIn('Invalid --label', str(error))
                self.assertEqual([], self.publish_dirs)
                self.assertEqual([], list((self.root / 'artifacts').iterdir()))

    def test_notices_match_the_framework_versions_inside_the_zip(self):
        error, output = self.run_package('--label', 'dev-runtime')
        self.assertIsNone(error, str(error))
        summary = json.loads(output)
        expected = {
            'DOTNET-LICENSE.txt': 'https://raw.githubusercontent.com/dotnet/runtime/v10.0.17/LICENSE.TXT',
            'DOTNET-THIRD-PARTY-NOTICES.txt': 'https://raw.githubusercontent.com/dotnet/runtime/v10.0.17/THIRD-PARTY-NOTICES.TXT',
            'WPF-LICENSE.txt': 'https://raw.githubusercontent.com/dotnet/wpf/v10.0.18/LICENSE.TXT',
        }
        self.assertEqual(list(expected.values()), self.urls)
        with zipfile.ZipFile(summary['archive']) as zipped:
            prefix = 'KenshiModManager-dev-runtime-win-x64/'
            runtimeconfig = json.loads(zipped.read(prefix + 'KenshiModManager.App.runtimeconfig.json'))
            self.assertEqual(self.frameworks, runtimeconfig['runtimeOptions']['includedFrameworks'])
            for name, url in expected.items():
                self.assertEqual(('notice: ' + url).encode(), zipped.read(prefix + name))
        self.assertEqual({entry['name']: entry['version'] for entry in self.frameworks},
                         summary['runtime_frameworks'])
        self.assertEqual(expected, summary['notice_urls'])

    def test_invalid_runtimeconfig_fails_actionably_before_downloading(self):
        valid = {'runtimeOptions': {'includedFrameworks': self.frameworks}}
        cases = [None, b'not json', b'[]', b'{}', b'{"runtimeOptions": []}',
                 b'{"runtimeOptions": {"frameworks": []}}',
                 b'{"runtimeOptions": {"includedFrameworks": null}}',
                 b'{"runtimeOptions": {"includedFrameworks": []}}',
                 b'{"runtimeOptions": {"includedFrameworks": [null]}}']
        for version in (None, '', '10.0', 'v10.0.17', ' 10.0.17', '10.0.17/../../main',
                        '10.0.17\n', '010.0.17', 17, True):
            cases.append(json.dumps({'runtimeOptions': {'includedFrameworks': [
                {'name': 'Microsoft.NETCore.App', 'version': version}, self.frameworks[1]
            ]}}).encode())
        cases.append(json.dumps({'runtimeOptions': {'includedFrameworks':
            [self.frameworks[0]]}}).encode())
        cases.append(json.dumps({'runtimeOptions': {'includedFrameworks':
            self.frameworks + [self.frameworks[0]]}}).encode())
        original = self.publish
        for index, data in enumerate(cases):
            with self.subTest(case=index, data=data):
                def invalid_publish(command, **kwargs):
                    original(command, **kwargs)
                    config = self.publish_dirs[-1] / 'KenshiModManager.App.runtimeconfig.json'
                    if data is None:
                        config.unlink()
                    else:
                        config.write_bytes(data)
                self.publish = invalid_publish
                self.urls.clear()
                error, _ = self.run_package('--label', f'dev-invalid-{index}')
                self.assertIsInstance(error, RuntimeError, str(error))
                self.assertIn('KenshiModManager.App.runtimeconfig.json', str(error))
                self.assertIn('includedFrameworks', str(error))
                self.assertIn('Re-publish', str(error))
                self.assertEqual([], self.urls)
                self.assertEqual([], list((self.root / 'artifacts').iterdir()))

    def test_fresh_staging_excludes_unrelated_files_and_is_removed(self):
        (self.root / 'artifacts' / 'stale.dll').write_bytes(b'stale')
        for label in ('dev-one', 'dev-two'):
            error, output = self.run_package('--label', label)
            self.assertIsNone(error, str(error))
            summary = json.loads(output)
            final = self.root / 'artifacts' / f'KenshiModManager-{label}-win-x64'
            self.assertNotEqual(final, self.publish_dirs[-1], 'Publish must use fresh staging')
            self.assertFalse(self.publish_dirs[-1].exists(), 'Staging must be cleaned')
            with zipfile.ZipFile(summary['archive']) as zipped:
                self.assertIsNone(zipped.testzip())
                self.assertFalse(any('stale.dll' in name or name.endswith('.pdb')
                                     for name in zipped.namelist()))
                self.assertEqual(b'resource', zipped.read(final.name + '/nested/resource.dll'))
        self.assertNotEqual(*self.publish_dirs)

    def test_failed_publish_leaves_no_partial_package(self):
        original = self.publish
        def failing_publish(command, **kwargs):
            original(command, **kwargs)
            raise RuntimeError('publisher failed')
        self.publish = failing_publish
        error, _ = self.run_package('--label', 'dev-failed')
        self.assertIsNotNone(error)
        self.assertEqual([], list((self.root / 'artifacts').iterdir()),
                         'A failed publish must leave no staging or delivered files')

    def test_existing_outputs_are_refused_before_publication(self):
        for label in ('V0.5', 'dev-finish'):
            targets = (
                f'artifacts/KenshiModManager-{label}-win-x64',
                f'artifacts/KenshiModManager-{label}-win-x64.zip',
                f'artifacts/SHA256SUMS-{label}.txt',
                f'.hermes/package-{label}.json',
            )
            for name in targets:
                with self.subTest(label=label, target=name):
                    target = self.root / name
                    target.write_bytes(b'keep exact bytes')
                    self.publish_dirs.clear()
                    try:
                        error, _ = self.run_package('--label', label)
                        self.assertIsNotNone(error, 'Existing outputs must not be overwritten')
                        self.assertIn('already exists', str(error))
                        self.assertIn('--label', str(error))
                        self.assertEqual([], self.publish_dirs)
                        self.assertEqual(b'keep exact bytes', target.read_bytes())
                    finally:
                        import shutil
                        for folder in ('artifacts', '.hermes'):
                            shutil.rmtree(self.root / folder)
                            (self.root / folder).mkdir()

    def test_development_label_keeps_app_version_and_preserves_v05(self):
        originals = {
            'artifacts/KenshiModManager-V0.5-win-x64/old.dll': b'shipped folder',
            'artifacts/KenshiModManager-V0.5-win-x64.zip': b'shipped zip',
            'artifacts/SHA256SUMS-V0.5.txt': b'shipped checksum',
            '.hermes/package-V0.5.json': b'shipped evidence',
        }
        for name, data in originals.items():
            path = self.root / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
        error, output = self.run_package('--label', 'dev-finish')
        self.assertIsNone(error, str(error))
        summary = json.loads(output)
        self.assertEqual('V0.5.1', summary['version'])
        self.assertEqual('dev-finish', summary['label'])
        archive = self.root / 'artifacts' / 'KenshiModManager-dev-finish-win-x64.zip'
        self.assertEqual(str(archive), summary['archive'])
        with zipfile.ZipFile(archive) as zipped:
            self.assertEqual(self.payload, zipped.read(
                'KenshiModManager-dev-finish-win-x64/KenshiModManager.App.exe'))
        for name, data in originals.items():
            self.assertEqual(data, (self.root / name).read_bytes(), name)


if __name__ == '__main__':
    unittest.main()
