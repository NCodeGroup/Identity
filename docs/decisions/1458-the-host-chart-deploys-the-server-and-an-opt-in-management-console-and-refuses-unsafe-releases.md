# 1458. The host chart deploys the server and an opt-in management console and refuses unsafe releases

- **Status:** Accepted
- **Date:** 2026-11-06
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0128](project-template/0128-liveness-never-checks-dependencies-readiness-does.md)

## Context

The Helm chart is the primary deployment artifact for `NCode.Identity.OpenId.Host`. The hosted platform and
self-hosters install the same chart
([ADR-1133](1133-the-hosted-platform-runs-only-the-published-artifacts-it-offers-self-hosters.md)). It has to run the
hardened image
([ADR-1248](1248-the-host-and-console-images-are-chiseled-non-root-multi-arch-digest-pinned-and-log-one-line-json.md))
as a rolling deployment behind an ingress. It also has to enforce the one rule the host cannot enforce itself: more than
one replica needs Valkey
([ADR-1164](1164-replicas-share-expiring-keys-values-and-indexes-through-ports-with-in-process-and-valkey-adapters.md)). The platform's values live in
a public repository
([ADR-1132](1132-platform-infrastructure-code-is-public-on-github-and-its-state-and-backups-live-in-aws.md)), so they
must never hold a secret.

The management console is a host and an origin of its own
([ADR-1437](1437-the-management-console-is-a-confidential-client-whose-own-bff-host-serves-only-the-console.md)). Its key rings are
PEM files ([ADR-1439](1439-a-bff-loads-its-configured-key-rings-at-start-and-refuses-frontends-naming-an-unknown-ring.md)). Its image is built
and published beside the host's, and an operator deploys both from one release. Issue
[#610](https://github.com/NCodeGroup/identity/issues/610) asks for the console in the chart.

## Decision

**One chart deploys the server, and the management console as an opt-in component, and it refuses at render time any
release the host or the console would refuse.**

1. **The chart lives at `charts/ncode-identity-host`.** It sits beside the host in the same repository, outside the
   host's project directory, so the Web SDK's content globs and the image's build context never pick up its files. It
   changes in the same commit as a setting. It needs Kubernetes 1.30 or later (`kubeVersion: ">=1.30.0-0"`), because
   the drain uses the kubelet's `sleep` action.
2. **Secrets are referenced, never passed.** The database connection (a finished connection string, or the parts of a
   Postgres operator's Secret,
   [ADR-1509](1509-the-host-chart-passes-a-postgres-operators-secret-as-the-connections-parts-and-the-host-assembles-the-npgsql-connection.md)),
   the Valkey connection (a finished connection string, or the address and password parts of a Valkey operator's Secret,
   [ADR-1510](1510-the-host-chart-passes-a-valkey-servers-address-and-password-as-parts-and-the-adapter-assembles-the-connection.md)),
   the root key, previous root key and recovery Secrets or ConfigMap
   ([ADR-1235](1235-stored-secrets-and-grant-payloads-are-protected-by-a-key-hierarchy-under-an-external-root-key.md)),
   and the console's key Secrets come from objects the operator names. No value in the chart is a secret, so a values
   file can be committed.
3. **Rendering fails, so `helm install` applies nothing,** instead of leaving a pod in a crash loop, when:
   - `allowedHosts` is empty or contains `*`;
   - the probe host would be a wildcard;
   - neither `database.existingSecret` nor `database.parts.existingSecret` is named;
   - a connection names both its connection-string Secret and its parts Secret, or a parts Secret leaves its host,
     database, user or password key empty;
   - a Valkey connection (`valkey` or `managementConsole.valkey`) names both its connection-string Secret and its parts
     Secret, or its parts Secret leaves the host key empty;
   - the `StaticSingle` strategy has no `tenantResolution.staticSingle.issuer` (the schema requires `https://`);
   - `secretProtection.rootKeyProvider` is empty;
   - `replicaCount` is above 1 without `valkey.existingSecret` or `valkey.parts.existingSecret`;
   - a root key provider's key reference is missing or malformed.

   In Production, rendering also fails on the `ephemeral` provider, on a `local` provider without
   `secretProtection.local.keySecret`, and when there is no recovery decision (`recovery.publicKeyConfigMap` or
   `recovery.disabled`). `values.schema.json` rejects unknown and mistyped values, so a typo fails instead of being
   ignored.
4. **The update strategy follows the shared state.** With Valkey, a new replica becomes ready before an old one stops (a
   rolling update with `maxSurge: 1`, `maxUnavailable: 0`). Without Valkey, each replica keeps single-use state in its
   own process, so two replicas never run at once: the `Recreate` strategy starts the new pod only after the old one
   has terminated. A rolling update with `maxSurge: 0` would start the new pod while the old one still serves. A
   single-replica update therefore has downtime, and high availability comes from adding Valkey. The console's strategy
   follows its own Valkey the same way.
5. **Pods meet the restricted Pod Security Standard.** Each pod runs as UID 1654, drops every capability, cannot
   escalate privileges, uses the `RuntimeDefault` seccomp profile, has a read-only root filesystem with an `emptyDir` at
   `/tmp`, and has no service-account token. Secret files mount with mode `0440` under `fsGroup` 1654.
6. **Probes and draining match the host.** The pods serve HTTPS only, so the probes use `scheme: HTTPS`
   ([ADR-1504](1504-the-hosts-serve-https-only-and-verify-their-certificate-at-startup.md) holds the Certificate, the Service and the
   rest of the TLS contract). The startup and liveness probes call `/health/live`, and readiness calls
   `/health/ready`. The kubelet sends the pod's IP as `Host`, which `AllowedHosts` rejects, so each probe sends
   `probes.host`, or else the first allowed host. A terminating pod keeps serving for `preStopSleepSeconds` (10) through
   the kubelet `sleep` action, within a 45-second grace period.
7. **Availability defaults.** A PodDisruptionBudget allows one replica down, and topology spread places replicas across
   nodes.
8. **Settings are typed where the chart enforces them and open everywhere else.** The chart types the settings it
   validates: hosts, tenant resolution, forwarded headers and secrets. Every other setting passes through `extraEnv`
   and `extraEnvFrom`, so a new setting needs no chart change.
9. **The console is the `managementConsole` component, off until `managementConsole.enabled`.** It has its own
   Deployment, Service, token-less ServiceAccount, PodDisruptionBudget, Valkey Secret (`managementConsole.valkey`) and
   restricted pods. Its image serves HTTPS only on port 8443
   ([ADR-1248](1248-the-host-and-console-images-are-chiseled-non-root-multi-arch-digest-pinned-and-log-one-line-json.md)), and its pods carry the same TLS
   contract as the server's. Both images share the chart's `appVersion`, so one release deploys both at one version.
10. **The console's route and URLs are derived.** The route for `managementConsole.host`, an Ingress or an HTTPRoute
    ([ADR-1507](1507-the-host-chart-routes-to-its-https-only-pods-through-gateway-api-a-traefik-ingress-or-the-operators-own-ingress.md)), routes every path to the
    console. `Bff:Frontends:mgmt:Authority` is `managementConsole.authority`, defaulting to the StaticSingle issuer, and
    `Bff:Frontends:mgmt:PublicOrigin` is `https://<managementConsole.host>`. The chart sets
    `ManagementConsole__ManagementApiUrl` to the authority's origin plus `/api` and has no value for it.
11. **The chart refuses a console the console would refuse.** Rendering fails when `managementConsole.host` is empty or a
    wildcard, when the authority is not `https://`, when a key ring has no key, and when
    `managementConsole.replicaCount` is above 1 without Valkey. The key rings and key management services are
    [ADR-1485](1485-the-host-chart-enables-server-kms-vendors-and-holds-console-key-rings-under-the-consoles-identity.md)'s.
12. **CI checks the contract.** The `Host chart` workflow runs `helm lint --strict`, renders every release under `ci/`,
    and checks that each unsafe release fails to render.

## Options considered

- **A chart for the console.** Rejected: the console's host joins the server's release, both images stay on the chart's
  `appVersion`, and one install deploys both.
- **Sharing the server's Valkey.** Rejected: the console would reach the server's single-use state.
- **Sharing the server's ServiceAccount.** Rejected: it may carry the server's cloud identities.
- **Routing machine callers through the console's BFF.** Rejected: machine callers authenticate at the OP directly.
- **One component in the chart with the console's checks at render time** (chosen): the operator learns of an unsafe
  release before anything is applied.

## Consequences

- The operator names the database Secret (the operator's own `<cluster>-app` Secret works as is), the root-key
  material and, for more than one replica, a Valkey Secret. The chart's `README.md` lists them.
- An operator registers `mgmt-console` with `jwks_uri` `https://<host>/bff/jwks`.
- A router the chart does not configure, such as a Traefik `IngressRoute`, is the operator's to add.
- A single-replica release has downtime on each update.
- The `Host chart` workflow fails a change that makes an unsafe release render.
- The Publish artifacts workflow publishes the chart with the images as a signed OCI artifact
  ([ADR-1505](1505-every-merge-to-main-publishes-the-host-image-the-console-image-the-chart-and-the-openapi-document-at-one-prerelease-version.md)).

## References

- [project-template ADR-0128](project-template/0128-liveness-never-checks-dependencies-readiness-does.md)
- [ADR-1132](1132-platform-infrastructure-code-is-public-on-github-and-its-state-and-backups-live-in-aws.md),
  [ADR-1133](1133-the-hosted-platform-runs-only-the-published-artifacts-it-offers-self-hosters.md),
  [ADR-1164](1164-replicas-share-expiring-keys-values-and-indexes-through-ports-with-in-process-and-valkey-adapters.md),
  [ADR-1235](1235-stored-secrets-and-grant-payloads-are-protected-by-a-key-hierarchy-under-an-external-root-key.md),
  [ADR-1248](1248-the-host-and-console-images-are-chiseled-non-root-multi-arch-digest-pinned-and-log-one-line-json.md),
  [ADR-1437](1437-the-management-console-is-a-confidential-client-whose-own-bff-host-serves-only-the-console.md),
  [ADR-1439](1439-a-bff-loads-its-configured-key-rings-at-start-and-refuses-frontends-naming-an-unknown-ring.md)
- `charts/ncode-identity-host`, `.github/workflows/host-chart.yml`
- Issue [#610](https://github.com/NCodeGroup/identity/issues/610)
