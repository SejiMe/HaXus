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


## Ugnay - 0006: local infrastructure and test-only probes

Verified on 2026-11-24 with .NET SDK 10.0.102, Aspire CLI/bundle and hosting packages 17.0.0, Npgsql 10.0.4, Docker Engine 28.1.1 and Compose 2.35.1 on Windows with Linux/amd64 containers. [Image locks](../deploy/images.lock.json) record exact manifest digests for PostgreSQL 18.7-bookworm, Keycloak 26.8.2, OpenBao 2.7.3, n8n 2.48.8, Aspire Dashboard 17.0.0 and the pinned .NET build/runtime images.

| Command/check | Actual result |
|---|---|
| `dotnet restore HaXus.slnx --locked-mode` | Passed for all five projects |
| `dotnet build HaXus.slnx -c Release --no-restore` | Passed with zero warnings/errors |
| `python scripts/infrastructure.py generate` | Aspire-generated seven-service topology normalized to portable Compose; exact image pins and read-only service-specific mounts |
| `python tests/test_infrastructure_guards.py` | Four checks passed: image guard, no-build launch, persistent UAT stop, cleanup scope and structural invariants |
| `python scripts/verify-startup.py` | Required-setting failure, database-down readiness/liveness, Production health opt-in, absent Fleet routes, secret-safe logs and literal dotenv precedence passed |
| `python scripts/verify-template.py` | Full generation, compilation, binding, Mediator, cancellation, lifetime and production-tree regression passed |
| `python scripts/infrastructure.py verify` | Disposable real-service startup, mounted credentials, restricted roles, Kusa readiness, synthetic realm discovery, real outages/recovery and cleanup passed |
| OpenBao probe behavior | Present, forbidden, confirmed missing (HXI - MK01), sealed/unreachable (HXI - KU01), unseal and restart recovery passed; structured records include correlation IDs |
| Database outage | `/health` became 503, `/alive` stayed 200, readiness recovered after restart |
| Keycloak/OpenBao outages | Their test-only probes failed appropriately while Ugnay remained ready; both recovered |
| `python scripts/verify-source.py` | Source Ugnay, both PostgreSQL services, Keycloak/Kusa readiness, automatic development Raft bootstrap/unseal, absent Fleet routes and UUID cleanup passed |
| Independent cleanup | No verification application processes, probe containers or probe volumes remained; owned source listeners closed and canonical CLI selection stayed unchanged |

Passing local evidence directories: `.artifacts/startup/c4078607429c`, `.artifacts/template-verification/run-5bw7cm88`, `.artifacts/infrastructure/probe-8a1007491acc` (Compose) and `.artifacts/infrastructure/probe-0e2af3028c37` (final full source topology and CLI isolation). These ignored directories are not published. Raw service logs and bootstrap material are kept outside the checkout under restricted user access; successful runs remove their private material.

The uniquely tagged local packaging fixture had image ID `sha256:06f87707450ee9d99e939e5583f795aea6eb1bf14e8f29821e20aa9cea95f026`. It is **not a Jenkins release image and not release-image UAT evidence**. Earlier engine HTTP 500/disconnect failures were followed by scoped cleanup and successful complete runs. Real checks identified and corrected the OpenBao volume path to its image-owned directory and separated longer bootstrap timeouts from short availability probes. No image or package baseline was changed.

The aggregate entry point is `pwsh -File scripts/verify.ps1`; the commands above were run individually during implementation and corrections. No aggregate-run pass is claimed. Public documentation links and Git exclusions were checked before publication.

Ugnay - 0006 remains On-Hold. Outstanding acceptance: Jenkins-published immutable-image UAT, and real Keycloak/OpenBao-dependent workflows. Synthetic discovery/Transit probes do not implement sign-in, sessions, authorization, production key adapters or a production dispatcher gateway.

Suggested next task: Ugnay - 0007 - implement and verify the Jenkins release flow. Do not activate it automatically.

Verified infrastructure commit: `85fe68047b0fb1579037fa42a1eb0f7a17eec841`. It was pushed to the public SejiMe/HaXus main branch and the remote commit matched locally. This subsequent evidence-only commit records publication; use `git rev-parse HEAD` for the latest revision.
