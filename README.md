# HaXus

.NET 10 backend and Aspire 17 skeleton with a backend feature template. No business features are implemented.

## Prerequisites and setup

Install .NET SDK **10.0.102**, then run from the repository root:

```powershell
dotnet tool install Aspire.Cli --version 17.0.0 --tool-path .tools
./.tools/aspire.cmd setup
$env:PATH = (Join-Path (Get-Location) '.tools') + ';' + $env:PATH
dotnet restore HaXus.slnx --locked-mode
dotnet build HaXus.slnx --configuration Release --no-restore
```

On Linux/macOS, invoke `.tools/aspire` and prepend `.tools` to PATH using your shell. The local tooling directory is ignored by Git. Use `dotnet tool update` with the same version and tool path if already installed.

## Run

The host now requires PostgreSQL. Use the [local infrastructure launcher](docs/infrastructure.md) rather than launching an unconfigured host:

```powershell
if (-not (Test-Path .env.local)) { Copy-Item .env.example .env.local }
pwsh -File scripts/dev.ps1
```

The infrastructure is implemented locally but real-container verification is currently blocked by Docker engine availability. No release-image UAT or production deployment is claimed.

## Documentation

- [Setup and commands](docs/setup.md)
- [Local infrastructure and probes](docs/infrastructure.md)
- [Architecture and boundaries](docs/architecture.md)
- [Feature template](docs/feature-template.md)
- [Verification evidence](docs/verification.md)
- [Deployment placeholder](docs/deployment/README.md)
- [Agent guidance](AGENTS.md) and [resource inventory](.agents/README.md)
- [Third-party notices](docs/third-party-notices.md)

Suggested next task: Ugnay - 0006 - wire AppHost and local Compose UAT (published-image checks depend on Ugnay - 0007). This recommendation does not select or execute it.
