# Setup

Use .NET SDK 10.0.102 (global.json disables SDK roll-forward). All four projects target net10.0. Package versions are centralized and all project lock files are committed.

## Tooling

From the root, install `Aspire.Cli` version `17.0.0` with `dotnet tool install Aspire.Cli --version 17.0.0 --tool-path .tools`, then run `.tools/aspire.cmd setup`. Add `.tools` to the current shell's PATH. Tool/bundle extraction needs network access on first setup. AppHost uses `DnxPinned` to select the matching CLI package rather than silently accepting another version.

## Build and verify

```powershell
dotnet restore HaXus.slnx --locked-mode
dotnet build HaXus.slnx -c Release --no-restore
pwsh -File scripts/verify.ps1
```

Aspire development startup uses its default Debug build; the full solution Release build remains a separate required check. The verification script checks development health, absence of sample endpoints, production health exclusion, and AppHost-managed host startup. It uses the local HTTP launch profiles and stops its process trees afterward. Run it with ports 5190, 15888, 18889, and 18890 free. Logs are local under ignored `.artifacts/`. Do not publish Dashboard login tokens from logs.

The root README contains standalone and AppHost launch commands. Docker, a database, identity provider, and secret store are not needed for this skeleton. The AppHost HTTP launch profile permits unsecured transport only for its loopback development endpoints; Dashboard browser-token authentication remains enabled. This profile is not a deployment configuration. Supporting services and UAT wiring are deferred to Ugnay - 0006.

When dependencies intentionally change, update central versions, run `dotnet restore HaXus.slnx --force-evaluate`, review lock-file changes, then repeat the locked restore, Release build, and smoke checks. Do not solve compatibility failures by changing the pinned baseline without authorization.
