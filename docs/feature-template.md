# Backend feature template

The repository provides an item template named `haxus-feature`. It creates backend-owned scaffolding only. No production dispatcher implementation, business operation, route registration, authorization policy, or persistence is installed by the template.

## Install and generate

From the repository root, run:

```powershell
dotnet new install ./templates/haxus-feature
dotnet new haxus-feature -n Fleet --owner Hanay
```

Generate only when explicitly authorized for a feature. The Fleet example is a generation example, not an implemented HaXus capability. Keep verification examples under ignored artifacts rather than the production feature tree.

| Option | Behavior |
|---|---|
| `--owner` (`-ow`) | Required PascalCase feature owner |
| `-n` | Capability name |
| `--query` (`-q`) | Query only; selecting both flags emits both |
| `--command` (`-c`) | Command only; neither flag emits both |
| `--query-name` (`-qn`) | Override the action name without the Query suffix |
| `--command-name` (`-cn`) | Override the action name without the Command suffix |
| `--command-method` (`-cm`) | Post, Put, Patch, Delete; defaults to Post |
| `--folders-only` (`-fo`) | Empty Api and Application capability directories |

Default actions are GetFleet and CreateFleet. Put, Patch, and Delete default to ReplaceFleet, UpdateFleet, and DeleteFleet. Example: `dotnet new haxus-feature -n Fleet --owner Hanay -c --command-method Put`.

Output goes beneath `src/HaXus.Ugnay/Features/<Owner>/{Api,Application}/<Capability>/<Action>`. Each Api action contains action-prefixed Request, Endpoint, and Response files. Each Application action contains its Query/Command, Handler, and Result files. Folder-only placeholders are consumed by the template engine and are not emitted as files.

Generate a query first and later add its command with `-c`. The template engine refuses conflicting overwrites; do not use `--force` to add an operation beside edited source.

## Complete a generated slice

Records initially have no business fields. Add fields and explicit request-to-message/result-to-response mappings as part of the owning feature. API-local transport models stay in Api; deliberately shared wire contracts belong in HaXus.Contracts. Application messages/results remain server-only and contain no HTTP result or persistence objects.

Handlers implement Mediator's query/command interfaces and throw NotImplementedException until implemented. The host exposes only `IApplicationDispatcher`, with separate query/command overloads returning ValueTask and accepting cancellation. Generated endpoints forward that token.

GET/DELETE requests use parameter binding; POST/PUT/PATCH use JSON bodies. Each endpoint offers `Map(RouteGroupBuilder group, string pattern)`, requiring an explicit route pattern. Nothing discovers or registers these helpers automatically. A feature author must select routes and authorization and supply the production operation-scope/caller-context gateway and policy pipeline before exposing an operation. The test dispatcher is not production infrastructure.

Mediator.Abstractions 3.0.2 is referenced by the host. Mediator.SourceGenerator 3.0.2 is private to the verification executable; production composition remains deferred. No Mediator dependency is added to Contracts or Aspire projects.

## Verification

```powershell
python scripts/verify-template.py
```

Requires Python 3.10+ and the pinned .NET SDK; no Python packages are required. The runner installs into an isolated template cache, creates output under ignored `.artifacts/template-verification/`, and saves command logs there. It checks generation/options, empty folders, overwrite protection, real compilation/dispatch, cancellation, route/query/JSON binding with synthetic fixture fields, scoped lifetime/pipeline behavior, and disposal. It confirms the production feature tree is unchanged. The test HTTP host uses a dynamically allocated loopback port and stops before completion.

For the whole checkout, also run locked restore, the Release solution build, and `pwsh -File scripts/verify.ps1`.

Delivered by Ugnay - 0003. Suggested next task: Ugnay - 0006, with published-image UAT checks depending on Ugnay - 0007. No follow-up task is automatically selected.
