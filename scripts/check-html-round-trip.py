#!/usr/bin/env python3
"""Run issue #27 against mounted Skia controls. Requires a desktop session and .NET 10."""
import os
from pathlib import Path
import subprocess
import tempfile
import time

root = Path(__file__).resolve().parents[1]
project = root / 'RichEditBoxLite.TestApp/RichEditBoxLite.TestApp.csproj'
subprocess.run(['dotnet', 'build', str(project), '-f', 'net10.0-desktop',
                '-p:TargetFrameworks=net10.0-desktop', '--verbosity', 'quiet'], check=True)
output = Path(tempfile.mkdtemp(prefix='richedit-html-'))
app = project.parent / 'bin/Debug/net10.0-desktop/RichEditBoxLite.TestApp.dll'
with (output / 'app.log').open('w') as log:
    process = subprocess.Popen(['dotnet', str(app)], cwd=root,
        env=dict(os.environ, RICHEDIT_HTML_CHECK_OUTPUT=str(output)), stdout=log, stderr=subprocess.STDOUT)
    try:
        deadline = time.monotonic() + 30
        result = output / 'results.txt'
        while not result.exists() and process.poll() is None and time.monotonic() < deadline:
            time.sleep(0.1)
        if not result.exists():
            raise RuntimeError(f'Checks did not finish. See {output / "app.log"}')
        report = result.read_text()
        print(report)
        print(f'Browser rendering fixture and log: {output}')
        code = 1 if 'FAIL ' in report else 0
    finally:
        if process.poll() is None:
            process.terminate()
            try:
                process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait()
raise SystemExit(code)
