# HaXus

Initial .NET 10 backend and Aspire 17 skeleton. No business features are implemented.

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

```powershell
# Host alone; no external services required.
dotnet run --project src/HaXus.Ugnay --configuration Release --no-build
# Or start the single-resource Aspire skeleton.
dotnet run --project aspire/HaXus.AppHost
```

The host's Development launch profile uses `http://localhost:5190`. `/health` and `/alive` are Development-only operational endpoints. `/` and `/weatherforecast` return 404. Aspire development startup builds Debug output; the acceptance build above validates Release independently. AppHost starts only the `ugnay` resource and the local Aspire Dashboard; use the generated authenticated Dashboard URL printed at startup. Press Ctrl+C to stop.

## Documentation

- [Setup and commands](docs/setup.md)
- [Architecture and boundaries](docs/architecture.md)
- [Verification evidence](docs/verification.md)
- [Deployment placeholder](docs/deployment/README.md)
- [Agent guidance](AGENTS.md) and [resource inventory](.agents/README.md)
- [Third-party notices](docs/third-party-notices.md)

Suggested next task: Ugnay - 0003 â€” integrate and verify the backend feature template. This recommendation does not select or execute it.
