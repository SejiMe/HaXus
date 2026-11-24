#!/usr/bin/env python3
"""Compose generation, guarded UAT launch, and isolated infrastructure verification."""
import argparse
import json
import os
from pathlib import Path
import re
import socket
import shutil
import subprocess
import time
import urllib.error
import urllib.request
import uuid

ROOT = Path(__file__).resolve().parents[1]
PROBE = ROOT / 'tests/InfrastructureProbes/bin/Release/net10.0/InfrastructureProbes.dll'
LOCKS = json.loads((ROOT / 'deploy/images.lock.json').read_text(encoding='utf-8'))


def command(args, *, env=None, log=None, success=True, timeout=600, stream=False, cwd=None):
    try:
        if stream:
            with Path(log).open('w', encoding='utf-8') as output:
                result = subprocess.run([str(a) for a in args], cwd=cwd or ROOT, env=env, stdout=output, stderr=subprocess.STDOUT, timeout=timeout)
        else:
            result = subprocess.run([str(a) for a in args], cwd=cwd or ROOT, env=env, capture_output=True,
                                    text=True, encoding='utf-8', errors='replace', timeout=timeout)
    except subprocess.TimeoutExpired as error:
        if log and not stream:
            output = (error.stdout or b'') + (error.stderr or b'')
            Path(log).write_text(output.decode('utf-8', errors='replace') if isinstance(output, bytes) else output, encoding='utf-8')
        raise RuntimeError(f'Command timed out: {args[0]}; inspect the local log.') from None
    if log and not stream:
        Path(log).write_text(result.stdout + result.stderr, encoding='utf-8')
    if success and result.returncode:
        # Service output can contain credentials. Keep it in the private local log.
        raise RuntimeError(f'Command failed ({result.returncode}): {args[0]}; inspect the local log.')
    return result


def probe(action, secrets, url=None, expected=None, log=None):
    args = ['dotnet', PROBE, action, secrets]
    if url is not None:
        args.append(url)
    if expected is not None:
        args.append(expected)
    return command(args, log=log)


def locked_image(name):
    entry = LOCKS[name]
    return entry['image'] + '@' + entry['digest']


def generate():
    output = ROOT / '.artifacts/infrastructure-publish'
    output.mkdir(parents=True, exist_ok=True)
    cli = ROOT / ('.tools/aspire.cmd' if os.name == 'nt' else '.tools/aspire')
    command([cli, 'publish', '--apphost', 'aspire/HaXus.AppHost/HaXus.AppHost.csproj',
             '--output-path', output, '--non-interactive'], env={**os.environ, 'HAXUS_ENVIRONMENT': 'dev', 'HAXUS_SECRET_ROOT': str(output / 'placeholder-secrets')}, log=output / 'publish.log')
    source = output / 'docker-compose.yaml'
    result = command(['docker', 'compose', '-f', source, '--env-file', output / '.env', 'config',
                      '--format', 'json', '--no-interpolate', '--no-path-resolution'], log=output / 'generated-config.log')
    model = json.loads(result.stdout)
    model.pop('name', None)
    # Preserve publisher topology; normalize only image pins and portable mounts/names.
    names = {'application-db': 'postgres', 'support-db': 'postgres', 'keycloak': 'keycloak',
             'openbao': 'openbao', 'kusa': 'kusa', 'compose-dashboard': 'dashboard'}
    mounts = {
        'application-db': ['app-db/admin', 'app-db/init'],
        'support-db': ['support-db/admin', 'support-db/init'],
        'keycloak': ['keycloak/keycloak.conf'],
        'openbao': ['openbao/server.hcl', 'openbao/server.pem', 'openbao/server-key.pem'],
        'kusa': ['kusa/password', 'kusa/encryption-key'], 'ugnay': ['ugnay']}
    volume_names = {name: name.removeprefix('haxus-dev-') for name in model.get('volumes', {})}
    for name, service in model['services'].items():
        if name in names:
            service['image'] = locked_image(names[name])
        service.pop('build', None)
        service.pop('container_name', None)
        service.pop('ports', None)
        service['restart'] = 'unless-stopped'
        service['platform'] = 'linux/amd64'
        index = 0
        for mount in service.get('volumes', []):
            if mount['type'] == 'bind':
                mount['source'] = '${HAXUS_SECRET_ROOT}/' + mounts[name][index]
                mount['read_only'] = True
                index += 1
            else:
                mount['source'] = volume_names.get(mount['source'], mount['source'])
    for section in ['volumes', 'networks']:
        model[section] = {volume_names.get(name, name) if section == 'volumes' else name:
                          {key: value for key, value in options.items() if key != 'name'}
                          for name, options in model.get(section, {}).items()}
    dashboard = model['services']['compose-dashboard']
    dashboard['environment'] = {
        'ASPIRE_DASHBOARD_TELEMETRY_OPTOUT': 'true',
        'ASPIRE_DASHBOARD_CONFIG_FILE_PATH': '/run/secrets/dashboard.json',
        'ASPNETCORE_URLS': 'http://0.0.0.0:18888',
        'DASHBOARD__OTLP__GRPCENDPOINTURL': 'http://0.0.0.0:18889',
        'DASHBOARD__OTLP__HTTPENDPOINTURL': 'http://0.0.0.0:18890'}
    dashboard['volumes'] = [{'type': 'bind', 'source': '${HAXUS_SECRET_ROOT}/dashboard/config.json',
                              'target': '/run/secrets/dashboard.json', 'read_only': True}]
    for name in ['application-db', 'support-db']:
        model['services'][name]['healthcheck'] = {'test': ['CMD-SHELL', 'pg_isready -U postgres -d postgres'],
                                                   'interval': '3s', 'timeout': '3s', 'retries': 40}
    for name in ['keycloak', 'kusa']:
        model['services'][name]['depends_on']['support-db']['condition'] = 'service_healthy'
    model['services']['ugnay']['depends_on']['application-db']['condition'] = 'service_healthy'
    (ROOT / 'compose.yaml').write_text(json.dumps(model, indent=2) + '\n', encoding='utf-8')
    print('PASS generated Compose topology, locked images, and portable secret-file mounts', flush=True)


_CHOSEN_PORTS = set()

def free_port():
    with socket.socket() as listener:
        listener.bind(('127.0.0.1', 0))
        port = listener.getsockname()[1]
    if port in _CHOSEN_PORTS: return free_port()
    _CHOSEN_PORTS.add(port)
    return port


def wait_http(url, status=200, timeout=240):
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        try:
            with urllib.request.urlopen(url, timeout=4) as response:
                actual = response.status
        except urllib.error.HTTPError as error:
            actual = error.code
        except (OSError, TimeoutError):
            actual = None
        if actual == status:
            return
        time.sleep(1)
    raise RuntimeError(f'Local HTTP status did not reach {status}; inspect private logs.')


def compose(env, project, *args, extra=None, log=None, success=True):
    values = ['docker', 'compose', '-p', project, '-f', ROOT / 'compose.yaml',
              '-f', ROOT / 'deploy/compose/overrides/uat.json']
    if extra:
        values += ['-f', extra]
    return command(values + list(args), env=env, log=log, success=success, timeout=1800 if args and args[0] in ['up', 'pull'] else 600, stream=bool(log and args and args[0] in ['up', 'pull']))


def verify():
    run_id = 'probe-' + uuid.uuid4().hex[:12]
    private_parent = Path(os.environ.get('LOCALAPPDATA', Path.home() / '.local/share')) / 'HaXus/secrets'
    secrets = private_parent / run_id
    artifacts = ROOT / '.artifacts/infrastructure' / run_id
    artifacts.mkdir(parents=True)
    engine = command(['docker', 'ps', '--format', '{{.ID}}'], log=artifacts / 'engine.log', success=False, timeout=15)
    if engine.returncode != 0: raise RuntimeError('Docker engine unavailable; no probe stack started. See the local engine log.')
    probe('prepare', secrets, log=artifacts / 'prepare.log')
    ports = {name: free_port() for name in ['application-db', 'support-db', 'keycloak', 'openbao', 'kusa', 'ugnay', 'dashboard']}
    image = 'haxus-ugnay-probe:' + run_id
    env = {**os.environ, 'HAXUS_SECRET_ROOT': str(secrets).replace('\\', '/'),
           'HAXUS_SERVICE_ENV_ROOT': str(secrets / 'env').replace('\\', '/'), 'UGNAY_IMAGE': image,
           'UGNAY_PORT': str(ports['ugnay']), 'KEYCLOAK_PORT': str(ports['keycloak']),
           'OPENBAO_PORT': str(ports['openbao']), 'KUSA_PORT': str(ports['kusa']),
           'DASHBOARD_PORT': str(ports['dashboard'])}
    extra = artifacts / 'ports.json'
    extra.write_text(json.dumps({'services': {name: {'ports': [f'127.0.0.1:{ports[name]}:5432']}
                                           for name in ['application-db', 'support-db']}}), encoding='utf-8')
    prefix = ['docker', 'ps', '-aq', '--filter', 'label=com.docker.compose.project=' + run_id]
    logs = []
    def action(*args, log_name='compose.log', success=True):
        return compose(env, run_id, *args, extra=extra, log=artifacts / log_name, success=success)
    def run_probe(name, url, expected=None):
        path = artifacts / f'{len(logs):02}-{name}.jsonl'; logs.append(path)
        probe(name, secrets, url, expected, log=path)
    application = f'http://127.0.0.1:{ports["ugnay"]}'
    keycloak = f'http://127.0.0.1:{ports["keycloak"]}'
    bao = f'https://127.0.0.1:{ports["openbao"]}'
    started = False
    try:
        print('Building uniquely tagged local packaging fixture (not release-image UAT)', flush=True)
        command(['docker', 'build', '--platform', 'linux/amd64', '-f', 'deploy/Ugnay.Dockerfile', '-t', image, '.'], log=artifacts / 'image-build.log', stream=True)
        image_id = command(['docker', 'image', 'inspect', image, '--format', '{{.Id}}']).stdout.strip()
        (artifacts / 'image-id.txt').write_text(image_id + '\n', encoding='utf-8')
        action('config', '--quiet', log_name='config.log')
        started = True
        action('up', '-d', log_name='startup.log')
        wait_http(application + '/health')
        wait_http(application + '/alive')
        wait_http(application + '/fleet', 404)
        wait_http(keycloak + '/realms/master/.well-known/openid-configuration', timeout=600)
        wait_http(f'http://127.0.0.1:{ports["kusa"]}/healthz/readiness')
        run_probe('bao-bootstrap', bao)
        run_probe('bao-fixture', bao)
        run_probe('keycloak-bootstrap', keycloak)
        for role, server in [('haxus', 'application-db'), ('keycloak', 'support-db'), ('kusa', 'support-db')]:
            run_probe('database-role', f'tcp://127.0.0.1:{ports[server]}', role)
        print('PASS startup, mounted credentials, restricted database roles, and Kusa readiness', flush=True)
        run_probe('identity', keycloak, 'available')
        action('stop', 'keycloak')
        run_probe('identity', keycloak, 'unavailable')
        wait_http(application + '/health')
        action('start', 'keycloak')
        wait_http(keycloak + '/realms/haxus-probe/.well-known/openid-configuration', timeout=600)
        run_probe('identity', keycloak, 'available')
        run_probe('bao-check', bao, 'present')
        run_probe('bao-check', bao, 'forbidden')
        run_probe('bao-remove-key', bao)
        run_probe('bao-check', bao, 'missing')
        run_probe('bao-fixture', bao)
        run_probe('bao-seal', bao)
        run_probe('bao-check', bao, 'unavailable')
        wait_http(application + '/health')
        run_probe('bao-bootstrap', bao)
        run_probe('bao-check', bao, 'present')
        action('stop', 'openbao')
        run_probe('bao-check', bao, 'unavailable')
        wait_http(application + '/health')
        action('start', 'openbao')
        run_probe('bao-bootstrap', bao)
        run_probe('bao-check', bao, 'present')
        print('PASS real Keycloak/OpenBao outages, recovery, missing-key/forbidden distinction, and structured probe codes', flush=True)
        action('stop', 'application-db')
        wait_http(application + '/health', 503, timeout=30)
        wait_http(application + '/alive')
        action('start', 'application-db')
        wait_http(application + '/health')
        print('PASS database readiness loss and recovery; liveness remains healthy', flush=True)
        material = json.loads((secrets / 'material.json').read_text(encoding='utf-8'))
        for path in logs:
            text = path.read_text(encoding='utf-8')
            assert all(value not in text for value in material.values()), 'Probe diagnostics leaked bootstrap material'
            for line in text.splitlines():
                record = json.loads(line)
                assert record.get('correlationId') and record.get('dependency') and record.get('status')
    finally:
        if started:
            compose(env, run_id, 'logs', '--no-color', extra=extra, log=secrets / 'service-logs.log', success=False)
            # Removal is scoped to the unique project created by this invocation, never daily resources.
            assert re.fullmatch(r'probe-[0-9a-f]{12}', run_id)
            action('down', '--volumes', '--remove-orphans', log_name='cleanup.log')
            assert not command(prefix).stdout.strip(), 'Probe containers remain'
            remaining = command(['docker', 'volume', 'ls', '-q', '--filter', 'label=com.docker.compose.project=' + run_id]).stdout.strip()
            assert not remaining, 'Probe volumes remain'
    assert secrets.resolve().parent == private_parent.resolve() and secrets.name == run_id
    shutil.rmtree(secrets)
    (artifacts / 'result.json').write_text(json.dumps({'status': 'passed', 'project': run_id, 'imageId': image_id,
        'evidence': 'local packaging fixture and test-only probes; not Jenkins UAT'}, indent=2), encoding='utf-8')
    print('PASS disposable project cleanup; daily-development resources untouched', flush=True)
    print('Evidence:', artifacts.relative_to(ROOT), flush=True)


def cleanup_probe(run_id):
    if not re.fullmatch(r'probe-[0-9a-f]{12}', run_id):
        raise RuntimeError('Cleanup requires an exact recorded probe UUID, never a daily environment name.')
    artifacts = ROOT / '.artifacts/infrastructure' / run_id
    if not artifacts.is_dir() or not (artifacts / 'prepare.log').exists():
        raise RuntimeError('This checkout has no preparation record for that probe UUID.')
    parent = Path(os.environ.get('LOCALAPPDATA', Path.home() / '.local/share')) / 'HaXus/secrets'
    secrets = parent / run_id
    env = {**os.environ, 'HAXUS_SECRET_ROOT': str(secrets).replace('\\', '/'),
           'HAXUS_SERVICE_ENV_ROOT': str(secrets / 'env').replace('\\', '/'),
           'UGNAY_IMAGE': 'haxus-ugnay-probe:' + run_id}
    compose(env, run_id, 'down', '--volumes', '--remove-orphans', log=artifacts / 'cleanup-resume.log')
    assert not command(['docker', 'ps', '-aq', '--filter', 'label=com.docker.compose.project=' + run_id]).stdout.strip()
    assert not command(['docker', 'volume', 'ls', '-q', '--filter', 'label=com.docker.compose.project=' + run_id]).stdout.strip()
    if secrets.exists():
        assert secrets.resolve().parent == parent.resolve() and secrets.name == run_id
        shutil.rmtree(secrets)
    print('PASS cleanup for recorded probe UUID:', run_id)


def uat(action):
    image = os.environ.get('UGNAY_IMAGE', '')
    if not re.fullmatch(r'ghcr\.io/sejime/ugnay-[0-9]+(?:\.[0-9]+)*@sha256:[0-9a-f]{64}', image):
        raise RuntimeError('UAT requires an explicit ghcr.io/sejime/ugnay-<version>@sha256:<digest>; no tag/local fallback.')
    for name in ['HAXUS_SECRET_ROOT', 'HAXUS_SERVICE_ENV_ROOT']:
        if not os.environ.get(name): raise RuntimeError('Missing required configuration name: ' + name)
    logs = ROOT / '.artifacts/uat'
    logs.mkdir(parents=True, exist_ok=True)
    if action == 'up':
        compose(os.environ, 'haxus-uat', 'pull', log=logs / 'pull.log')
        compose(os.environ, 'haxus-uat', 'up', '-d', '--no-build', log=logs / 'startup.log')
    else:
        compose(os.environ, 'haxus-uat', 'down', log=logs / 'stop.log')  # Preserve persistent UAT data.
    print('UAT', action, 'completed')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=['generate', 'verify', 'uat-up', 'uat-down', 'cleanup-probe'])
    parser.add_argument('probe_id', nargs='?')
    args = parser.parse_args()
    if args.action == 'generate': generate()
    elif args.action == 'verify': verify()
    elif args.action == 'cleanup-probe': cleanup_probe(args.probe_id or '')
    else: uat(args.action.removeprefix('uat-'))
