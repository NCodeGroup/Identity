# 1134. The platform and the host follow written cloud-native operating principles

- **Status:** Accepted
- **Date:** 2026-10-18
- **Deciders:** NCode Group

## Context

The hosted platform ([ADR-1131](1131-hosted-platform-environment-model-and-hostname-scheme.md)) is provisioned as public
code ([ADR-1132](1132-platform-infrastructure-code-is-public-on-github-and-its-state-and-backups-live-in-aws.md)) from
the same artifacts offered to self-hosters
([ADR-1133](1133-the-hosted-platform-runs-only-the-published-artifacts-it-offers-self-hosters.md)). Many choices (how a
node is patched, where a cache lives, how a schema changes, whether a feature may keep in-memory state) need one shared
answer rather than being argued case by case. An identity provider is also an availability dependency for every relying
party: when it is down, nobody signs in.

## Decision

**The platform and the host follow these principles; a design that breaks one needs an ADR that says why.** Reviews cite
a principle by number.

1. **Cattle, not pets.** Every node, pod and cluster is disposable and rebuildable from code. Nothing is configured by
   hand; machines are replaced rather than patched in place. Interactive access (SSH, `kubectl exec`) is break-glass
   only, audited, and never a change mechanism.
2. **Immutable artifacts.** One signed artifact (image digest, chart, module release) is built once and promoted through
   lab, the production `canary` tenant and production. A change is a new release, never an in-place edit; containers
   run with read-only root filesystems.
3. **Everything as code, reconciled continuously.** Desired state lives in Git: OpenTofu for cloud resources and Flux for
   the cluster. A scheduled plan finds drift, which is reconciled rather than hand-fixed. The only manual steps are the
   documented bootstrap ones.
4. **Stateless services.** Processes keep no state that matters beyond a request. Durable state lives in PostgreSQL,
   shared ephemeral state in the shared-state port (`NCode.Identity.SharedState`) backed by Valkey
   ([ADR-1164](1164-replicas-share-single-use-state-through-an-atomic-expiring-key-set.md)), and keys under the
   secret-protection hierarchy or a KMS. Any replica serves any request: no sticky sessions. Configuration comes from the
   environment, logs go to stdout, and processes start fast and shut down gracefully.
5. **Redundant by default.** Every service runs at least two replicas spread across nodes, with PodDisruptionBudgets and
   readiness gates. Calls use timeouts and bounded retries with jitter. Remaining single points of failure are
   accepted explicitly, not by accident.
6. **Recoverable and proven.** Backups are encrypted, immutable and off-vendor. Restores and full rebuilds are drilled
   against written RPO and RTO targets.
7. **Secure by default.** Least privilege everywhere, OIDC or workload identity instead of long-lived credentials, no
   secrets in Git, encryption in transit and at rest, only signed artifacts admitted, and default-deny network policy.
8. **Observable.** Metrics, logs and traces flow through OpenTelemetry, with service-level objectives and symptom-based
   alerts.
9. **Small, safe, automated changes.** Rollouts are progressive and halt on failed readiness. Schema changes expand and
   contract so the old and new releases run side by side
   ([ADR-1180](1180-each-host-contexts-migrations-run-before-the-rollout-and-only-expand-the-schema.md)).
10. **Portable by open standards.** Kubernetes, OCI, OpenTelemetry, PostgreSQL and S3 are the interfaces, so a vendor is
    replaced by swapping a component module.
11. **Cost-aware.** Sizes come from measurements, and every vendor has a budget alert.

## Options considered

- **No written principles; decide case by case.** Each decision stays locally reasonable while the whole drifts (a
  hand-patched node here, an in-memory store there). Rejected.
- **Principles as guidance only, without the ADR requirement for exceptions.** Exceptions accumulate silently. Rejected:
  an exception is fine, but it is explicit.
- **Written principles, with an ADR for every exception.** Chosen.

## Consequences

- The host's code is correct across interchangeable replicas; the production coding conventions carry that rule
  ([`csharp-production.instructions.md`](../../.github/instructions/csharp-production.instructions.md) §4).
- The in-process `IJobDispatcher` loses in-flight jobs when a replica is replaced
  ([#25](https://github.com/NCodeGroup/identity/issues/25)), so a durable adapter is a prerequisite before the platform
  offers CIBA push or back-channel logout.
- The accepted single points of failure are one region and a non-HA managed control plane
  ([hosting analysis §2](../hosting-analysis.md#2-summary-and-ranked-recommendations)).
- A reviewer cites a principle by number instead of re-arguing it.

## References

- [The Twelve-Factor App](https://12factor.net/).
- [Hosting analysis](../hosting-analysis.md): §5 HA, §11 delivery, §13 pre-launch considerations.
- [ADR-1131](1131-hosted-platform-environment-model-and-hostname-scheme.md),
  [ADR-1132](1132-platform-infrastructure-code-is-public-on-github-and-its-state-and-backups-live-in-aws.md),
  [ADR-1133](1133-the-hosted-platform-runs-only-the-published-artifacts-it-offers-self-hosters.md),
  [ADR-1164](1164-replicas-share-single-use-state-through-an-atomic-expiring-key-set.md),
  [ADR-1180](1180-each-host-contexts-migrations-run-before-the-rollout-and-only-expand-the-schema.md).
