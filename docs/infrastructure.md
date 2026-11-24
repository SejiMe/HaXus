# Local infrastructure and isolated probes

## Current implementation and verification state

Ugnay - 0006 adds the topology, launchers, database readiness, Compose packaging, and test-only probes. Build, startup/configuration, generated Compose, and real-container dependency probes pass. The disposable stack verified mounted credentials, restricted database roles, dependency outages, recovery and cleanup. See [verification evidence](verification.md).

Actual Jenkins-image UAT also remains pending Ugnay - 0007. Real identity/key-dependent workflows and the production dispatcher/security pipeline remain deferred.

## Pinned topology

AppHost is the topology owner: Ugnay from source, application PostgreSQL, supporting PostgreSQL, Keycloak, OpenBao and Kusa. Supporting PostgreSQL has separate restricted Keycloak/Kusa roles and databases. The application database has a separate haxus role. No application entities or migrations are added.

`deploy/images.lock.json` records Linux/amd64 manifest digests for PostgreSQL 18.7-bookworm, Keycloak 26.8.2, OpenBao 2.7.3, n8n 2.48.8, Aspire Dashboard 17.0.0, .NET SDK 10.0.102 and ASP.NET runtime 10.0.2. The Dashboard uses the canonical mcr.microsoft.com/aspire/dashboard repository. The Dockerfiles use pinned base digests; Kusa's Dockerfile is the thin pinned-base recipe, and AppHost uses the equivalent upstream digest directly.

## Daily development

Current launch/verification tooling targets Windows with PowerShell 7; containers target Linux/amd64. Prerequisites: .NET SDK 10.0.102, local Aspire CLI/bundle 17.0.0, a healthy Docker Linux engine, Python 3.10+ and PowerShell 7. `docker ps` must succeed first.

```powershell
if (-not (Test-Path .env.local)) { Copy-Item .env.example .env.local }
pwsh -File scripts/dev.ps1
```

The launcher imports `.env.local` as literal KEY=VALUE data, strips optional surrounding quotes, and never evaluates shell expressions or expands substitutions. Existing process variables win. Restart AppHost after edits.

Default environment is dev. Development material lives outside the checkout at `%LOCALAPPDATA%/HaXus/secrets/dev` on Windows. The preparation tool restricts access to the current account and SYSTEM, generates development-only credentials and TLS material, and reuses existing material. Do not change stored database credentials without a coordinated rotation; persistent databases do not reset when files change.

Daily PostgreSQL/OpenBao/Kusa data survives container recreation in environment-named volumes. AppHost waits only for the application database before starting Ugnay. Keycloak, OpenBao, Kusa and Dashboard are not application-readiness prerequisites.

OpenBao runs as a persistent Raft server with a local certificate, never dev-server mode. The development hook initializes a fresh store with development-only bootstrap material and unseals it. It refuses an initialized store without its original material and refuses to replace existing bootstrap material for an unexpectedly uninitialized store. A bootstrap failure is reported without gating Ugnay's own database readiness. This single-share local bootstrap is **not a production unseal or recovery policy**. Never run this development bootstrap against production.

Press Ctrl+C to stop development. Preserve development volumes and private material. A separately managed AppHost can be stopped with the Aspire CLI using its exact project path and PID; never stop all AppHosts to clean up a test.

## Database readiness

`ConnectionStrings:haxus` is required. Missing configuration fails startup and logs the setting name without its value. Connection strings are validated without echoing supplied values. Readiness opens a bounded Npgsql connection and runs SELECT 1; there is no application schema or business persistence.

- `/health`: application database readiness.
- `/alive`: process liveness; database outages do not make it unhealthy.
- Development exposes both routes. Other environments require explicit `HealthChecks:Enabled=true`; the local container configuration opts in.
- Ordinary Production defaults still expose neither route. There are no Fleet or other business routes.

## Compose generation and UAT

```powershell
python scripts/infrastructure.py generate
```

The generator invokes Aspire publish, reads its generated configuration through Docker Compose, and writes root `compose.yaml` as JSON-formatted YAML (valid Compose). It preserves service topology while normalizing portable mounts, exact image pins, and project-scoped names. Intentional UAT loopback ports and per-service env files live in `deploy/compose/overrides/uat.json`.

Copy `.env.uat.example` to `.env.uat` and supply a real `ghcr.io/sejime/ugnay-<version>@sha256:<digest>`, `HAXUS_SECRET_ROOT`, and `HAXUS_SERVICE_ENV_ROOT`. The service-env directory must contain one non-secret env file per service. Prepare service-specific startup credentials through protected files, not chat or Git. The development preparation tool can generate separate synthetic local-UAT material; it is not a production secret provisioner.

```powershell
pwsh -File scripts/uat.ps1 up
pwsh -File scripts/uat.ps1 down
```

The UAT wrapper imports `.env.uat` into the child process, preserving existing process-variable precedence. Mutable tags, other repositories and local-image fallbacks are rejected before Compose runs. Startup pulls the explicit release image and uses no local build; stopping preserves volumes.

Ugnay mounts only its key-per-file directory; PostgreSQL gets its password/init files; Keycloak gets its protected config; Kusa gets its database-password/encryption-key files; OpenBao gets only its config/certificate/key; Dashboard gets its protected auth configuration. The exporter OTLP API key is supplied to Ugnay through its protected configuration file. No credential is intentionally embedded in generated Compose or images.

Do not publish rendered private configuration, login tokens or raw service logs. Mounted startup credentials and restricted database-role access passed in the disposable Linux/amd64 stack on the documented Windows host.

## Repeatable checks

```powershell
python scripts/verify-startup.py
python tests/test_infrastructure_guards.py
python scripts/verify-template.py
python scripts/infrastructure.py verify
python scripts/verify-source.py
# Or run the full sequence:
pwsh -File scripts/verify.ps1
```

The container runner uses a UUID-owned Compose project, credentials, networks, volumes and loopback ports. Its uniquely tagged local Ugnay image is a packaging fixture, **not Jenkins release-image UAT**. It tests restricted roles, dependency outages/recovery, readiness/liveness, and secret-file wiring. A separate source verifier starts AppHost with UUID-owned names and development material.

The standalone .NET probe executable is outside production source. It creates a synthetic Keycloak realm and Transit key only in the disposable stack. Confirmed key absence maps to test-only HXI - MK01; sealed/unreachable OpenBao maps to HXI - KU01. Forbidden requests remain distinct. Structured records contain dependency, status, code and correlation ID, never credentials or key material. Discovery probes do not verify actual sign-in/session/authorization workflows.

Successful checks remove only their UUID-owned containers, volumes and private material. Failed runs preserve private diagnostics for investigation. Raw service and detached-start logs stay in the protected private directory; safe result metadata and command logs stay under ignored `.artifacts/`. A failed or unavailable engine is a verification failure, never a deployment pass.

## Recovery and handoff

For Docker engine errors, restore Docker Desktop/its Linux backend until `docker ps` succeeds; then rerun the full checks. If a prior disposable run was interrupted, use `python scripts/infrastructure.py cleanup-probe <recorded-probe-uuid>`; the helper requires that exact UUID to have a local preparation record and cannot target dev/UAT. Earlier engine-interrupted runs were cleaned up by their recorded UUIDs after recovery; the subsequent complete probe run passed. Do not factory-reset Docker or delete persistent volumes to make a test pass. For missing OpenBao bootstrap material, stop and recover the original material rather than initializing over an existing store.

Keep Ugnay - 0006 On-Hold for Jenkins-published immutable-image UAT and real-workflow verification. Suggested next task: Ugnay - 0007. Do not activate it automatically.

## Upstream references

[Aspire Compose generation](https://aspire.dev/deployment/docker-compose/), [AppHost eventing](https://aspire.dev/app-host/eventing/), [Dashboard configuration](https://aspire.dev/dashboard/configuration/), and the [OpenBao image Dockerfile](https://github.com/openbao/openbao/blob/0439ef5812538733afb277b4445a54706edc1db7/Dockerfile). OpenBao uses its image-owned `/openbao/file` directory for the Raft volume while remaining unprivileged. Bootstrap operations have longer bounded timeouts than the four-second availability probes.
