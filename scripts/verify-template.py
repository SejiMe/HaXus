#!/usr/bin/env python3
"""Verify generation and runtime behavior without changing application features."""
from pathlib import Path
import re
import subprocess
import tempfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
ARTIFACTS = ROOT / '.artifacts/template-verification'
ARTIFACTS.mkdir(parents=True, exist_ok=True)
RUN = Path(tempfile.mkdtemp(prefix='run-', dir=ARTIFACTS))
HIVE = RUN / 'hive'
COUNT = 0


def check(condition, message):
    if not condition:
        raise AssertionError(message)


def run(args, success=True):
    global COUNT
    result = subprocess.run([str(a) for a in args], cwd=ROOT, text=True, encoding='utf-8', errors='replace', capture_output=True)
    COUNT += 1
    (RUN / f'command-{COUNT:02}.log').write_text(result.stdout + result.stderr, encoding='utf-8')
    check((result.returncode == 0) == success,
          f'Unexpected exit {result.returncode}: {args}\n{result.stdout}\n{result.stderr}')
    return result


def generate(name, *options, success=True):
    output = RUN / name
    run(['dotnet', 'new', 'haxus-feature', '-n', 'Fleet', '--owner', 'Hanay',
         '--output', output, *options, '--debug:custom-hive', HIVE], success=success)
    return output


def files(directory):
    return {p.relative_to(directory).as_posix(): p.read_bytes() for p in directory.rglob('*') if p.is_file()}


def action_files(action, message):
    base = 'src/HaXus.Ugnay/Features/Hanay/'
    return {f'{base}Api/Fleet/{action}/{action}{suffix}.cs' for suffix in ['Request', 'Endpoint', 'Response']} | {
        f'{base}Application/Fleet/{action}/{action}{suffix}.cs' for suffix in [message, 'Handler', 'Result']}


before = files(ROOT / 'src/HaXus.Ugnay/Features')
run(['dotnet', 'new', 'install', ROOT / 'templates/haxus-feature', '--debug:custom-hive', HIVE])
default = generate('default')
expected = action_files('GetFleet', 'Query') | action_files('CreateFleet', 'Command')
check(set(files(default)) == expected, 'Default paths or action-prefixed filenames differ')
for name, data in files(default).items():
    text = data.decode()
    check(not re.search(r'OwnerToken|FeatureToken|QueryActionToken|CommandActionToken|CommandMethodToken', text), name)
    check('namespace HaXus.Ugnay.Features.Hanay.' in text, name)
print('PASS default layout, names, namespaces, and payload selection', flush=True)

check(set(files(generate('query', '-q'))) == action_files('GetFleet', 'Query'), 'Query-only selection')
check(set(files(generate('command', '-c'))) == action_files('CreateFleet', 'Command'), 'Command-only selection')
check(set(files(generate('both', '-q', '-c'))) == expected, 'Both action flags')
renamed = generate('renamed', '--query-name', 'FindFleet', '--command-name', 'BookFleet')
check(set(files(renamed)) == action_files('FindFleet', 'Query') | action_files('BookFleet', 'Command'), 'Name overrides')
for method, verb in [('Post', 'Create'), ('Put', 'Replace'), ('Patch', 'Update'), ('Delete', 'Delete')]:
    output = generate('method-' + method, '-c', '--command-method', method)
    check(set(files(output)) == action_files(verb + 'Fleet', 'Command'), method + ' action naming')
    endpoint = next(output.rglob('*Endpoint.cs')).read_text(encoding='utf-8')
    check('HttpMethods.' + method in endpoint, method + ' HTTP mapping')
    check(('[AsParameters]' if method == 'Delete' else '[FromBody]') in endpoint, method + ' binding')
folders = generate('folders', '--folders-only')
check(not files(folders), 'Folders-only emitted placeholder files')
base = folders / 'src/HaXus.Ugnay/Features/Hanay'
check({p.relative_to(base).as_posix() for p in base.rglob('*') if p.is_dir()} ==
      {'Api', 'Api/Fleet', 'Application', 'Application/Fleet'}, 'Folders-only tree')
run(['dotnet', 'new', 'haxus-feature', '-n', 'Fleet', '--output', RUN / 'missing-owner',
     '--debug:custom-hive', HIVE], success=False)
generate('invalid-method', '--command-method', 'Unknown', success=False)
generate('invalid-option', '--unknown-option', success=False)
alias_output = RUN / 'short-aliases'
run(['dotnet', 'new', 'haxus-feature', '-n', 'Fleet', '-ow', 'Hanay', '-q', '-c',
     '-qn', 'LookupFleet', '-cn', 'ReviseFleet', '-cm', 'Patch', '-o', alias_output,
     '--debug:custom-hive', HIVE])
check(set(files(alias_output)) == action_files('LookupFleet', 'Query') | action_files('ReviseFleet', 'Command'), 'Short aliases')
check('HttpMethods.Patch' in next(alias_output.rglob('ReviseFleetEndpoint.cs')).read_text(encoding='utf-8'), 'Method alias')
check(set(files(generate('long-actions', '--query', '--command'))) == expected, 'Long action aliases')
check(not files(generate('short-folders', '-fo')), 'Short folder alias')
print('PASS flags, aliases, overrides, HTTP methods, empty folders, and invalid inputs', flush=True)

incremental = generate('incremental', '-q')
query = next(incremental.rglob('GetFleetQuery.cs'))
query.write_text(query.read_text(encoding='utf-8') + '\n// Edited by a feature author.\n', encoding='utf-8')
query_bytes = query.read_bytes()
generate('incremental', '-c')
check(query.read_bytes() == query_bytes, 'Adding a command changed the edited query')
previous = files(incremental)
generate('incremental', '-q', success=False)
check(files(incremental) == previous, 'Conflicting generation changed existing files')
print('PASS incremental generation and overwrite refusal', flush=True)

fixture = RUN / 'fixture'
run(['dotnet', 'new', 'haxus-feature', '-n', 'Fleet', '--owner', 'Hanay', '--output', fixture,
     '--debug:custom-hive', HIVE])
# Synthetic fields exist only in the ignored fixture, never in the shipped payload.
for capability, method, action, message in [
        ('BindingGet', None, 'GetBindingGet', 'Query'),
        ('BindingPost', 'Post', 'CreateBindingPost', 'Command'),
        ('BindingPut', 'Put', 'ReplaceBindingPut', 'Command'),
        ('BindingPatch', 'Patch', 'UpdateBindingPatch', 'Command'),
        ('BindingDelete', 'Delete', 'DeleteBindingDelete', 'Command')]:
    options = ['-q'] if method is None else ['-c', '--command-method', method]
    run(['dotnet', 'new', 'haxus-feature', '-n', capability, '--owner', 'Hanay', '--output', fixture,
         *options, '--debug:custom-hive', HIVE])
    for p in fixture.rglob(action + '*.cs'):
        text = p.read_text(encoding='utf-8')
        if p.name.endswith('Request.cs'):
            text = text.replace(f'record {action}Request;', f'record {action}Request {{ public string? Probe {{ get; init; }} }}')
        elif p.name.endswith('Response.cs'):
            text = text.replace(f'record {action}Response;', f'record {action}Response(string? Probe);')
        elif p.name.endswith('Result.cs'):
            text = text.replace(f'record {action}Result;', f'record {action}Result(string? Probe);')
        elif p.name.endswith(message + '.cs'):
            text = text.replace(f'record {action}{message} :', f'record {action}{message}(string? Probe) :')
        elif p.name.endswith('Endpoint.cs'):
            text = text.replace('_ = await', 'var result = await')
            text = text.replace(f'new {action}{message}()', f'new {action}{message}(request.Probe)')
            text = text.replace(f'new {action}Response()', f'new {action}Response(result.Probe)')
        elif p.name.endswith('Handler.cs'):
            text = 'using TemplateVerification;\n' + text
            text = text.replace(f'class {action}Handler :', f'class {action}Handler(ScopeProbe scope) :')
            text = text.replace(f'=> throw new NotImplementedException("Implement {action} before registering its endpoint.");',
                f'''{{
        cancellationToken.ThrowIfCancellationRequested();
        if (!scope.PipelineVisited) throw new InvalidOperationException("Pipeline scope was bypassed.");
        return ValueTask.FromResult(new {action}Result(message.Probe));
    }}''')
        p.write_text(text, encoding='utf-8', newline='\n')

project = ET.Element('Project', {'Sdk': 'Microsoft.NET.Sdk.Web'})
group = ET.SubElement(project, 'ItemGroup')
ET.SubElement(group, 'ProjectReference', {'Include': str(ROOT / 'src/HaXus.Ugnay/HaXus.Ugnay.csproj')})
ET.SubElement(group, 'PackageReference', {'Include': 'Mediator.SourceGenerator', 'PrivateAssets': 'all',
    'IncludeAssets': 'runtime; build; native; contentfiles; analyzers; buildtransitive'})
ET.SubElement(group, 'Compile', {'Include': str(ROOT / 'tests/TemplateVerification/Program.cs'), 'Link': 'Program.cs'})
ET.indent(project, space='  ')
(fixture / 'TemplateChecks.csproj').write_text(ET.tostring(project, encoding='unicode'), encoding='utf-8')
run(['dotnet', 'run', '--project', fixture / 'TemplateChecks.csproj', '-c', 'Release'])
print('PASS generated-code build, test-host binding, Mediator dispatch, cancellation, and scoped disposal', flush=True)
check(files(ROOT / 'src/HaXus.Ugnay/Features') == before, 'Verification changed production feature files')
check(not list((ROOT / 'src/HaXus.Ugnay/Features').rglob('*Fleet*')), 'Production Fleet feature is present')
print('PASS production feature tree unchanged; no generated Fleet installed', flush=True)
print('Evidence:', RUN.relative_to(ROOT), flush=True)
