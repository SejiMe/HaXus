# Verification

Verification history for the initial skeleton and backend feature template. Both were verified on 2026-11-23. The initial sections below record Ugnay - 0010; the final section records Ugnay - 0003.

## Baseline

- .NET SDK 10.0.102; all four projects target net10.0.
- Aspire CLI, Aspire.Hosting.AppHost, and Aspire.Hosting.Dotnet: 17.0.0.
- Standard ServiceDefaults: Microsoft.Extensions.Http.Resilience and Microsoft.Extensions.ServiceDiscovery 10.10.0; OpenTelemetry packages 1.18.0, matching the Aspire 17 template.
- Exact direct versions are centralized; all four project lock files are committed.

## Passed checks

| Check | Result |
|---|---|
| `dotnet restore HaXus.slnx` | All four projects restored |
| `dotnet build HaXus.slnx -c Release --no-restore` | Passed; zero warnings and zero errors |
| `dotnet restore HaXus.slnx --locked-mode` | Passed |
| `pwsh -File scripts/verify.ps1` | Development health/alive 200; Production health/alive 404; root and weatherforecast 404 in all checks |
| AppHost startup | Single ugnay application launched and health/alive returned 200; normal development startup builds Debug output |
| Cleanup | Verification process trees stopped; loopback ports released |
| Static structure checks | Exactly four source projects and four lock files; central package versions; no Shared or Android project |
| Documentation and exclusions | Local Markdown links resolved; credentials/local settings, logs, tooling and build outputs excluded by Git rules |

The first AppHost smoke attempt exposed a Release/no-build invocation mismatch with the development CLI. The documented AppHost command now uses its standard Debug startup. The HTTP development profile explicitly allows loopback unsecured transport while retaining Dashboard authentication. Neither correction changes the pinned .NET/Aspire baseline.

## Publication

Repository: https://github.com/SejiMe/HaXus. GitHub confirmed PUBLIC visibility and default branch main. Origin is https://github.com/SejiMe/HaXus.git.

Verified skeleton commit: `f3ddd93a06d14835bcf12b44f1d1045a1f2c5a78`. The initial remote main matched this local commit. This subsequent documentation-only commit records publication evidence; obtain the current checkout revision with `git rev-parse HEAD`.

## Initial skeleton limits

At completion of Ugnay - 0010, the checkout had no dispatcher contract or feature template. Readiness checked only the standard self check. The subsequent template work below supersedes that setup limitation without adding business features, external dependency readiness, production diagnostics, deployment, or Android.

## Ugnay - 0003: backend feature template

### Delivered

- Installable `haxus-feature` item template with saved configuration, CLI aliases, action-prefixed files, and genuinely empty folders-only output.
- Host-owned IApplicationDispatcher contract with query/command overloads, ValueTask results, and optional cancellation tokens.
- Mediator.Abstractions and Mediator.SourceGenerator pinned to 3.0.2. The production host references only Abstractions; SourceGenerator and the dispatcher implementation are confined to isolated verification.
- Empty local transport/application records, explicit NotImplementedException handlers, explicit route-pattern mapping helpers, and method-appropriate binding. No automatic production route registration.

### Passed checks

| Check | Result |
|---|---|
| `python scripts/verify-template.py` | Passed all generation and runtime checks |
| Default and selective generation | Exact owner/capability/action paths, prefixed filenames and namespaces; query, command, both, all aliases and name overrides |
| HTTP methods | GET queries; Post/Put/Patch/Delete commands with matching default action names and binding |
| Folders-only | Api/Application capability directories created without emitted placeholder files |
| Invalid inputs | Missing owner, invalid method, and unknown option rejected |
| Existing source | Adding a command preserved an edited query byte-for-byte; conflicting generation refused without changes |
| Compiled test host | Real generated Mediator dispatch and scoped pipeline execution passed for all verbs |
| Binding and cancellation | Route/query/JSON values round-tripped using test-only fields; missing/malformed JSON rejected; original endpoint cancellation tokens forwarded and canceled dispatch observed |
| Lifetime | Separate operation scopes and asynchronous scoped dependency disposal verified, including failing operations |
| Production tree | Unchanged by template checks; no Fleet source installed or feature routes exposed |
| Locked restore and Release build | Passed; solution build had zero warnings and zero errors |
| Operational smoke checks | Standalone Development, Production and AppHost passed; Fleet paths returned 404; verification listeners stopped |
| Normal installation | `dotnet new install ./templates/haxus-feature` succeeded |

The operational cleanup check now inspects active IPv4/IPv6 listeners and waits for all verification ports to close, avoiding false failures from TCP shutdown/TIME_WAIT state.

Local template logs: `.artifacts/template-verification/run-b2yma84w/`. Operational logs: `.artifacts/verification/`. Both are ignored and not published. The generated test host and template cache remain isolated under ignored artifacts.

### Publication and limits

Verified integration commit: `e142e3f0d626fee395894dace448288950cae7f7`. Local HEAD and remote main matched after the public push, with a clean working tree. This subsequent documentation-only commit records that publication evidence; use `git rev-parse HEAD` for the latest revision. The production operation-scope/caller-context gateway, runtime policy pipeline, business features and external services remain deferred. The fixture dispatcher provides no production security guarantee.

Suggested next task: Ugnay - 0006 - wire AppHost and local Compose UAT. Its published-image UAT checks depend on Ugnay - 0007. Neither task is selected automatically.
