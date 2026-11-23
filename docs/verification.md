# Verification

Verified on 2026-11-23 for Ugnay - 0010.

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

Target: https://github.com/SejiMe/HaXus, public, default branch main. Remote publication and source commit evidence will be recorded after the verified skeleton is pushed.

## Limits and next task

This verifies an operational skeleton, not application features, dependency readiness, production diagnostics, deployment, or Android. No business API, contracts, entities, migrations, dispatcher or feature template is implemented. Readiness is only the standard self check.

Suggested next task: Ugnay - 0003 — confirm the dispatcher contract, integrate the saved feature template, and verify generation/build using isolated examples. It remains unselected. Supporting-service orchestration and UAT remain with Ugnay - 0006.
