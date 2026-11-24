#!/usr/bin/env python3
"""Non-container startup/readiness and literal dotenv checks."""
import os
from pathlib import Path
import socket
import subprocess
import time
import urllib.error
import urllib.request
import uuid

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / '.artifacts/startup' / uuid.uuid4().hex[:12]
OUT.mkdir(parents=True)
APP = ROOT / 'src/HaXus.Ugnay/bin/Release/net10.0/HaXus.Ugnay.dll'
base = {key: value for key, value in os.environ.items()
        if key.casefold() not in ['connectionstrings__haxus', 'haxus_app_secrets', 'healthchecks__enabled']}
base['HAXUS_APP_SECRETS'] = str(OUT / 'absent')
base['DOTNET_ENVIRONMENT'] = 'Production'
base['ASPNETCORE_ENVIRONMENT'] = 'Production'
result = subprocess.run(['dotnet', APP], cwd=ROOT, env=base, capture_output=True, text=True, timeout=15)
assert result.returncode != 0 and 'ConnectionStrings:haxus' in result.stdout + result.stderr
(OUT / 'missing-setting.log').write_text(result.stdout + result.stderr, encoding='utf-8')
print('PASS missing required configuration fails startup with the setting name', flush=True)

for environment, expose, readiness, alive in [('Development', False, 503, 200), ('Production', False, 404, 404), ('Production', True, 503, 200)]:
    with socket.socket() as listener:
        listener.bind(('127.0.0.1', 0)); port = listener.getsockname()[1]
    marker = 'startup-test-not-a-real-credential'
    env = {**base, 'DOTNET_ENVIRONMENT': environment, 'ASPNETCORE_ENVIRONMENT': environment,
           'ASPNETCORE_URLS': f'http://127.0.0.1:{port}',
           'ConnectionStrings__haxus': f'Host=127.0.0.1;Port=1;Database=haxus;Username=haxus;Password={marker};Timeout=1',
           'HealthChecks__Enabled': str(expose).lower()}
    path = OUT / f'{environment}-{expose}.log'
    with path.open('w', encoding='utf-8') as log:
        process = subprocess.Popen(['dotnet', APP], cwd=ROOT, env=env, stdout=log, stderr=log,
                                   creationflags=subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0)
        def status(route):
            try:
                with urllib.request.urlopen(f'http://127.0.0.1:{port}' + route, timeout=8) as response: return response.status
            except urllib.error.HTTPError as error: return error.code
        try:
            deadline = time.monotonic() + 20
            while True:
                assert process.poll() is None, 'Host exited before verification'
                try:
                    assert status('/health') == readiness
                    break
                except (OSError, TimeoutError):
                    if time.monotonic() > deadline: raise
                    time.sleep(.2)
            assert status('/alive') == alive
            assert status('/fleet') == 404 and status('/api/hanay/fleet') == 404
        finally:
            process.terminate()
            try: process.wait(timeout=10)
            except subprocess.TimeoutExpired: process.kill(); process.wait()
    assert marker not in path.read_text(encoding='utf-8'), 'A connection credential was logged'
print('PASS database-down readiness, independent liveness, production health opt-in, absent feature routes, and credential-safe logs', flush=True)

dotenv = OUT / 'values.env'
dotenv.write_text('HAXUS_TEST_PRECEDENCE=file\nHAXUS_TEST_LITERAL="$(throw evil)"\n', encoding='utf-8')
test = OUT / 'dotenv-check.ps1'
test.write_text('''$ErrorActionPreference = 'Stop'
. $args[0]
$env:HAXUS_TEST_PRECEDENCE = 'process'
Import-HaxusEnvironment $args[1]
if ($env:HAXUS_TEST_PRECEDENCE -ne 'process') { throw 'Process precedence lost' }
if ($env:HAXUS_TEST_LITERAL -ne '$(throw evil)') { throw 'Literal environment value changed' }
Write-Output 'PASS literal dotenv values and existing process-variable precedence'
''', encoding='utf-8')
result = subprocess.run(['pwsh', '-NoProfile', '-File', test, ROOT / 'scripts/Environment.ps1', dotenv],
                        capture_output=True, text=True, timeout=15)
assert result.returncode == 0, result.stderr
print(result.stdout.strip(), flush=True)
print('Evidence:', OUT.relative_to(ROOT), flush=True)
