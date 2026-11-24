"""Non-container checks for release-image guards and generated configuration."""
import importlib.util
import json
import os
from pathlib import Path
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('infrastructure', ROOT / 'scripts/infrastructure.py')
infra = importlib.util.module_from_spec(spec)
spec.loader.exec_module(infra)


class InfrastructureGuards(unittest.TestCase):
    def test_mutable_and_wrong_repository_images_never_start_compose(self):
        for image in ['', 'haxus-probe:latest', 'ghcr.io/sejime/ugnay-1.0:staging',
                      'ghcr.io/other/ugnay-1.0@sha256:' + 'a' * 64]:
            with self.subTest(image=image), patch.dict(os.environ, {'UGNAY_IMAGE': image}), patch.object(infra, 'compose') as compose:
                with self.assertRaises(RuntimeError): infra.uat('up')
                compose.assert_not_called()

    def test_release_uses_digest_without_build_and_stop_preserves_data(self):
        env = {'UGNAY_IMAGE': 'ghcr.io/sejime/ugnay-1.0.0@sha256:' + 'a' * 64,
               'HAXUS_SECRET_ROOT': 'operator-provided-path', 'HAXUS_SERVICE_ENV_ROOT': 'operator-provided-path'}
        with patch.dict(os.environ, env), patch.object(infra, 'compose') as compose, patch('builtins.print'):
            infra.uat('up')
            calls = [call.args[2:] for call in compose.call_args_list]
            self.assertEqual(calls, [('pull',), ('up', '-d', '--no-build')])
            compose.reset_mock()
            infra.uat('down')
            self.assertEqual(compose.call_args.args[2:], ('down',))

    def test_cleanup_refuses_daily_and_unrecorded_environments(self):
        for name in ['dev', 'haxus-uat', 'probe-123', 'probe-' + '0' * 12]:
            with self.subTest(name=name), patch.object(infra, 'compose') as compose:
                with self.assertRaises(RuntimeError): infra.cleanup_probe(name)
                compose.assert_not_called()

    def test_generated_topology_keeps_pins_and_private_mounts(self):
        model = json.loads((ROOT / 'compose.yaml').read_text(encoding='utf-8'))
        names = {'application-db', 'support-db', 'keycloak', 'openbao', 'kusa', 'ugnay', 'compose-dashboard'}
        self.assertEqual(set(model['services']), names)
        for options in model.get('volumes', {}).values(): self.assertNotIn('name', options)
        for name, service in model['services'].items():
            self.assertNotIn('container_name', service)
            self.assertEqual(service['platform'], 'linux/amd64')
            if name != 'ugnay': self.assertIn('@sha256:', service['image'])
            for mount in service.get('volumes', []):
                if mount['type'] == 'bind':
                    self.assertTrue(mount['read_only'])
                    self.assertTrue(mount['source'].startswith('${HAXUS_SECRET_ROOT}/'))
        for name in ['application-db', 'support-db']:
            env = model['services'][name]['environment']
            self.assertNotIn('POSTGRES_PASSWORD', env)
            self.assertEqual(env['POSTGRES_PASSWORD_FILE'], '/run/secrets/admin')
        self.assertEqual(set(model['services']['ugnay']['depends_on']), {'application-db'})


if __name__ == '__main__':
    unittest.main()
