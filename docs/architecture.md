# Architecture

## Projects and dependencies

| Project | Responsibility | References |
|---|---|---|
| HaXus.Ugnay | ASP.NET Core backend host; future co-hosted web entry point | Contracts, ServiceDefaults |
| HaXus.Contracts | Empty library reserved for deliberate shared wire contracts | None |
| HaXus.ServiceDefaults | Standard resilience, service discovery, health and OpenTelemetry setup | ASP.NET shared framework and pinned standard defaults packages |
| HaXus.AppHost | Development orchestration for the single ugnay resource | Aspire.Hosting.AppHost and Aspire.Hosting.Dotnet 17.0.0 |

AppHost uses `AddDotnetProject` with the Ugnay project path. It uses Microsoft.NET.Sdk and has no application ProjectReference or dependency on the obsolete Aspire.AppHost.Sdk. Build the entire solution: building AppHost alone does not build the path-referenced application.

The Ugnay host references neither Shared nor Android. Contracts references neither host nor infrastructure. AppHost is not shipped as the production application.

## Structure and template adaptation

The selected Minimal Web API/vertical-slice plan is a documentation baseline, not an existing source checkout to clone. The initial host therefore uses minimal ASP.NET Core startup, with sample routes omitted. The standard ServiceDefaults source and dependency pins come from the official Aspire 17.0.0 template package; see third-party notices.

Core, Kandado, and Hanay have empty Api, Application, and UI branches. These record ownership and entry boundaries, not capabilities. Infrastructure and common-interface directories likewise contain placeholders. No custom dispatcher, Mediator, persistence, security, Serilog integration, or product behavior is implemented. Standard OpenTelemetry export activates only when an OTLP endpoint is configured.

The only operational HTTP routes are Development-only `/health` and `/alive`. Readiness currently checks only the standard self check; it does not verify any external dependency. No public business API or wire types are introduced.

## Subsequent work

Suggested next task: Ugnay - 0003 — confirm the host dispatcher contract, install the backend feature template, and verify generated operations in isolated fixtures. The reserved templates/haxus-feature directory does not yet provide an installable template.

Ugnay - 0006 owns external service topology and Compose UAT; Ugnay - 0007 owns release automation; Ugnay - 0008 owns telemetry/recovery validation; Ugnay - 0009 owns later Android release verification. No follow-up task is automatically selected by this handoff.

Shared Razor UI, Android, feature handlers/endpoints/entities, migrations, authentication, supporting services, and deployment remain deferred. Tests/ currently reserves future business tests; the skeleton is validated with restore/build and operational smoke checks.
