# 179. The host chart references its secrets and refuses a release the host would refuse

- **Status:** Accepted
- **Date:** 2026-10-20
- **Deciders:** NCode Group

## Context

The Helm chart is the primary deployment artifact for `NCode.Identity.OpenId.Host`. The hosted platform and
self-hosters install the same chart ([ADR-0133](0133-we-ship-and-run-the-same-deployment-artifacts.md)). It has to run
the hardened image ([ADR-0178](0178-the-host-image-is-chiseled-non-root-multi-arch-and-carries-the-login-bundle.md)) as
a rolling deployment behind an ingress. It also has to enforce the one rule the host cannot enforce itself: more than
one replica needs Valkey ([ADR-0164](0164-replicas-share-single-use-state-through-an-atomic-expiring-key-set.md)).
The platform's values live in a public repository
([ADR-0132](0132-platform-infrastructure-as-code-is-public-on-github.md)), so they must never hold a secret.

## Decision

1. **The chart lives at `charts/ncode-identity-host`.** It sits in the same repository as the host and changes in the
   same commit as a setting. It is kept out of the host's project directory, so the Web SDK's content globs and the
   image's build context never pick up its files.
2. **Secrets are referenced, never passed.** The database and Valkey connection strings come from Secrets the operator
   names. So do the key-protection certificates
   ([ADR-0177](0177-the-data-protection-key-ring-is-encrypted-at-rest-in-production.md)), which are
   `kubernetes.io/tls` Secrets such as cert-manager issues. No value in the chart is a secret, so a values file can be
   committed.
3. **The chart refuses at render time any release the host would refuse, or one that cannot share state.** Rendering
   fails when:
   - `allowedHosts` is empty or contains `*`;
   - the probes' `Host` header would be a wildcard;
   - no database Secret is named;
   - the `StaticSingle` strategy has no `https` issuer;
   - Production has no key-protection certificate;
   - more than one replica runs without Valkey.

   `values.schema.json` rejects unknown and mistyped values, so a typo fails instead of being ignored. `helm install`
   fails before anything is applied, rather than leaving a pod in a crash loop.
4. **The update strategy follows the shared state.** With Valkey, a new replica becomes ready before an old one stops
   (`maxSurge: 1`, `maxUnavailable: 0`). Without Valkey, each replica keeps single-use state in its own process, so two
   replicas never run at once, not even during an update (`maxSurge: 0`, `maxUnavailable: 1`). A single-replica release
   therefore has downtime on each update, and high availability comes from adding Valkey.
5. **Pods meet the restricted Pod Security Standard.** Each pod:
   - runs as the image's non-root UID 1654;
   - drops every capability and cannot escalate privileges;
   - uses the `RuntimeDefault` seccomp profile;
   - has a read-only root filesystem, with an `emptyDir` at `/tmp`;
   - has no service-account token, because the host never calls the Kubernetes API.

   Key-protection Secrets mount with mode `0440` under `fsGroup` 1654, so only the app's group can read the private
   key.
6. **Probes and draining match the host.**
   - The startup and liveness probes call `/health/live`, so a lost database restarts nothing.
   - The readiness probe calls `/health/ready`.
   - The kubelet sends the pod's IP as the `Host` header, and `AllowedHosts` rejects it with 400. So each probe sends
     `probes.host`, or else the first allowed host, and rendering fails when that host is a wildcard.
   - A terminating pod keeps serving for `preStopSleepSeconds` (10 seconds) while the ingress removes it, and then
     shuts down gracefully within a 45-second grace period.
   - The image has no shell, so the drain uses the kubelet's `sleep` action. That action is why the chart requires
     Kubernetes 1.30 or later.
7. **Availability defaults.** A PodDisruptionBudget allows one replica down at a time, and replicas spread across nodes
   when the scheduler can place them that way.
8. **Settings are typed where the chart enforces them and open everywhere else.** The chart models the settings it
   validates: the host names, the tenant resolution, the reverse proxy, and the secrets. Every other host setting is
   passed through `extraEnv` and `extraEnvFrom`, so a new setting needs no chart change.
9. **CI checks the contract.** The `Host chart` workflow runs `helm lint --strict` and renders each release under
   `ci/`. It also checks that each unsafe release fails to render.

## Consequences

- An operator creates three Secrets before the first install: the database, the key-protection certificate, and Valkey
  for more than one replica. The chart's `README.md` lists them.
- The chart migrates the database in a pre-deploy Job
  ([ADR-0180](0180-migrations-run-before-the-rollout-and-keep-the-previous-release-working.md)). It does not yet publish
  itself: `F48` tracks publishing it with the image as a signed OCI artifact.
- An ingress other than the chart's generic `Ingress`, such as a Traefik `IngressRoute`, is the operator's to add.
