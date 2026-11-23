# HaXus agent guidance

This repository contains the backend/Aspire skeleton and the feature template delivered by Ugnay - 0003; business features remain unimplemented. Work on one explicitly authorized task at a time. Task IDs in handoffs use plain text.

Read [architecture](docs/architecture.md), [setup](docs/setup.md), and [.agents inventory](.agents/README.md) before changes. Preserve the .NET 10.0.102 and Aspire 17.0.0 pins unless a task authorizes an upgrade. Central package versions live in Directory.Packages.props; update lock files intentionally and verify locked restore.

Keep HTTP adapters in Features/<Owner>/Api, shared guarded operations in Application, and host-specific web adapters in UI. Empty branches are placeholders, not implemented features. Contracts must not reference host or infrastructure projects. AppHost is development tooling, not an application runtime dependency.

Use the documented Release solution build and `pwsh -File scripts/verify.ps1`. Record actual checks and limits. Do not add business endpoints, entities, migrations, shared UI, Android, service integrations, Compose or release automation as incidental setup work.

Read [feature-template guidance](docs/feature-template.md) before scaffolding. IApplicationDispatcher is a contract only; the production gateway and policy pipeline remain unimplemented. Generated handlers fail explicitly and routes require deliberate registration. Use `python scripts/verify-template.py` for isolated template checks; do not add verification Fleet slices to production. Supporting-service topology/UAT belong to Ugnay - 0006. Do not activate either task automatically. Keep secrets, private project material, machine-specific paths, and logs out of commits. No project-owned agent skills are implemented yet.
