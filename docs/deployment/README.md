# Deployment and local UAT

Local infrastructure wiring and Compose preparation are implemented under Ugnay - 0006. Local disposable-container probes pass; production deployment and Jenkins release-image UAT remain unverified.

See [infrastructure instructions](../infrastructure.md) for generation, secret-file delivery, loopback UAT configuration, digest validation, verification and recovery. Root compose.yaml is generated from AppHost; intentional environment differences stay under deploy/compose/overrides. Dockerfiles live directly under deploy.

Ugnay - 0007 owns publishing the linux/amd64 release candidate and its immutable digest. Ugnay - 0006 remains open until that image has been exercised in isolated UAT, together with the remaining real-workflow checks. Production rollout, credential rotation and recovery verification remain separate follow-up work.
