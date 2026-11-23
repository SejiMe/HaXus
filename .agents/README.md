# Agent resources

No project-owned skills or workflows are installed in this checkout. The Ugnay-owned backend item template is available at templates/haxus-feature; use it when scaffolding an explicitly authorized backend operation. Its isolated verification runner is scripts/verify-template.py. See [template guidance](../docs/feature-template.md). AGENTS.md supplies repository guidance; docs/ supplies public setup and architecture references.

Shared prerequisites: the agent's machine-level task and documentation workflows, .NET SDK 10.0.102, Git, and the local Aspire CLI 17.0.0 bundle. Machine-level skills are not copied into this repository.

Planned capabilities include vertical slices, identity/access, data/migrations, web/Android UI, deployment/diagnostics, and automation workflows. They become available only when their owned skill files are implemented and verified.

For every future skill addition, record its relative path, owner, trigger, and upstream source/pinned revision when imported. Review imported instructions and scripts before adoption. Keep this inventory current.
