# 134. Cloud-native operating principles: cattle, immutable, stateless, redundant

- **Status:** Accepted (principle 4 amended by
  [ADR-0164](0164-replicas-share-single-use-state-through-an-atomic-expiring-key-set.md):
  shared ephemeral state lives in the shared-state port, backed by Valkey)
- **Date:** 2026-10-18
- **Deciders:** NCode Group

## Context

The hosted platform ([ADR-0131](0131-hosted-platform-environment-model-and-hostname-scheme.md)) is provisioned as public
code ([ADR-0132](0132-platform-infrastructure-as-code-is-public-on-github.md)) from the same artifacts offered to
self-hosters ([ADR-0133](0133-we-ship-and-run-the-same-deployment-artifacts.md)). Many later choices — how a node is
patched, where a cache lives, how a schema changes, whether a feature may keep in-memory state — need one shared answer
rather than being argued case by case. An identity provider is also an availability dependency for every relying party:
when it is down, nobody signs in.

## Decision

**The platform and the host follow these principles; a design that breaks one needs an ADR that says why.**

1. **Cattle, not pets.** Every node, pod, and cluster is disposable and rebuildable from code. Nothing is configured by
   hand; machines are replaced rather than patched in place (node-pool upgrades roll new nodes); interactive access
   (SSH, `kubectl exec`) is break-glass only, audited, and never a change mechanism.
2. **Immutable infrastructure and artifacts.** Build once, then promote the **same** signed artifact — image digest,
   chart, module release — through lab, the production `canary` tenant, and production. A change is a new release,
   never an in-place edit; containers run with read-only root filesystems.
3. **Everything as code, reconciled continuously.** Desired state lives in Git (OpenTofu for cloud resources, Flux for
   the cluster); drift is detected (scheduled `plan`) and reconciled, not hand-fixed. The only manual steps are the
   documented bootstrap ones.
4. **Stateless services.** Processes keep no state that matters beyond a request: durable state lives in PostgreSQL,
   shared ephemeral state in `IDistributedCache` (Valkey), keys in the secret store or KMS. Any replica serves any
   request — no sticky sessions, no local-disk writes for durable data. Configuration comes from the environment;
   logs go to stdout; processes start fast and shut down gracefully (twelve-factor).
5. **Redundant by default; design for failure.** At least two replicas of every service, spread across nodes (and zones
   where the vendor has them), with PodDisruptionBudgets and readiness gates; the platform survives the loss of any one
   node. Calls between services use timeouts, bounded retries with jitter, and degrade gracefully. Remaining single
   points of failure are documented and accepted explicitly, not by accident.
6. **Recoverable, and proven so.** Backups are encrypted, immutable, and off-vendor; restores and full rebuilds from code
   plus backups are drilled on a schedule against written RPO / RTO targets.
7. **Secure by default.** Least privilege everywhere; no long-lived credentials (OIDC / workload identity); secrets never
   in Git; encryption in transit and at rest; only signed artifacts are admitted; default-deny network policies.
8. **Observable.** Metrics, logs, and traces through OpenTelemetry; service-level objectives with error budgets; alerts
   on user-visible symptoms rather than causes.
9. **Small, safe, automated changes.** Every change passes the automated gates; rollouts are progressive (canary
   tenant, rolling updates that halt on failed readiness, fast rollback); schema changes are expand/contract so the old
   and new versions run side by side.
10. **Portable by standards.** Prefer open interfaces — Kubernetes, OCI, OpenTelemetry, the PostgreSQL protocol, the S3
    API — so a vendor can be replaced by swapping a component module rather than redesigning.
11. **Cost-aware.** Size from measurements, not guesses; every vendor has a budget alert.

## Options considered

- **No written principles; decide case by case.** Each decision stays locally reasonable while the whole drifts
  (a hand-patched node here, an in-memory store there). Rejected.
- **Principles as guidance only, without the ADR requirement for exceptions.** Exceptions would accumulate silently.
  Rejected: an exception is fine, but it must be explicit.

## Consequences

- The host's code must be correct with several interchangeable replicas; the production coding conventions carry that
  rule ([`csharp-production.instructions.md`](../../.github/instructions/csharp-production.instructions.md) §4).
- Known gaps are tracked, not ignored:
  - the in-process `IJobDispatcher` loses in-flight jobs when a replica is replaced ([#25](https://github.com/NCodeGroup/Identity/issues/25)) — a durable adapter is a
    prerequisite before the platform offers CIBA push or back-channel logout;
  - readiness, graceful drain, and disruption budgets are part of the host's deployment artifacts ([#85](https://github.com/NCodeGroup/Identity/issues/85));
  - the Data Protection key ring must be encrypted outside the database
    ([ADR-0177](0177-the-data-protection-key-ring-is-encrypted-at-rest-in-production.md));
  - accepted single points of failure at launch: one region, and the free (non-HA) managed control plane on the
    rank-1 topology ([hosting analysis §2](../hosting-analysis.md#2-summary-and-ranked-recommendations)).
- Reviews can cite a principle by number instead of re-arguing it.

## References

- [The Twelve-Factor App](https://12factor.net/).
- [Hosting analysis](../hosting-analysis.md) — §5 HA, §11 delivery, §13 pre-launch considerations.
