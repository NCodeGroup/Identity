# 1458. The management console ships as its own image and an opt-in component of the host chart

- **Status:** Accepted (amends
  [ADR-1437](1437-the-management-console-is-a-confidential-client-served-by-a-bff-host-of-its-own.md),
  [ADR-1179](1179-the-host-chart-references-its-secrets-and-refuses-a-release-the-host-would-refuse.md) and
  [ADR-1249](1249-the-host-images-base-images-are-pinned-by-digest-and-tracked-by-dependabot.md); amended by
  [ADR-1485](1485-the-host-chart-holds-the-consoles-key-rings-in-a-key-management-service-under-the-consoles-own-identity.md):
  the console's key rings are `managementConsole.keyRings.client` and `managementConsole.keyRings.dpop`, each a PEM
  Secret, keys a key management service holds, or both, reached through the console's own workload identity, and the
  chart refuses a key ring without a key; amended by
  [ADR-1488](1488-the-management-console-reaches-the-management-api-at-its-authoritys-origin-and-its-host-serves-only-the-console.md):
  the console's Ingress routes every path to the console, and `ManagementConsole:ManagementApiUrl` is `/api` on the
  authority's origin)
- **Date:** 2026-11-06
- **Deciders:** NCode Group

## Context

The management console is a host and an origin of its own
([ADR-1437](1437-the-management-console-is-a-confidential-client-served-by-a-bff-host-of-its-own.md)). `manage.<apex>`
routes `/api` to the OP and every other path to the console. The console's key rings are PEM files
([ADR-1439](1439-a-bff-loads-the-key-rings-its-configuration-names-from-pem-files.md)). The image and the chart are the
deployable artifacts of a host
([ADR-1178](1178-the-host-image-is-chiseled-non-root-multi-arch-and-carries-the-login-bundle.md),
[ADR-1179](1179-the-host-chart-references-its-secrets-and-refuses-a-release-the-host-would-refuse.md)). Issue
[#610](https://github.com/NCodeGroup/Identity/issues/610) asks for both.

## Decision

**The console ships as a container image of its own, and as an opt-in component of the host chart that routes the manage
host.**

1. **The image mirrors the host image.** `src/NCode.Identity.ManagementConsole.Host/Dockerfile` uses the same pinned
   base images and digests. Its Node stage builds `@ncode/app-mgmt`, its final stage is chiseled-extra and runs as the
   non-root UID 1654 on port 8080, it writes one-line JSON logs, and it serves the bundle from `/app/mgmt`.
2. **A `Console image` workflow builds it.** The `Host image` workflow's job is the required check `Host image`, and
   the `Console image` workflow's job is `Build console image`. Both workflows are triggered alike, including
   `merge_group`.
3. **Dependabot's docker stream lists both Dockerfile directories.** One pull request moves the base-image digests of
   both images.
4. **The chart carries the console as the `managementConsole` component.** It is off until `managementConsole.enabled`.
   It has its own Deployment, Service and ServiceAccount (no token), its own Valkey Secret, the restricted Pod Security
   Standard, a PodDisruptionBudget, and an Ingress for `managementConsole.host` that routes `/api` to the server's
   Service and `/` to the console's. `Bff:Frontends:mgmt:Authority` defaults to the StaticSingle issuer and
   `ManagementConsole:ManagementApiUrl` to `https://<managementConsole.host>/api`. The console's two key rings are
   mounted from the Secrets named by `managementConsole.keys.clientKeySecret` (`mgmt-client.pem`) and
   `managementConsole.keys.dpopKeySecret` (`mgmt-dpop.pem`).
5. **The chart refuses a release the console would refuse.** It fails when `managementConsole.host` is empty or holds a
   wildcard, when `allowedHosts` does not list the host, when the authority is empty or not `https://`, when either key
   Secret is unnamed, and when `managementConsole.replicaCount` is above one without `managementConsole.valkey`.

## Options considered

- **A chart of its own.** Rejected: the manage host's Ingress joins the server's Service and the console's Service, the
  server must admit the manage host in `allowedHosts`, and one release keeps both images on the chart's `appVersion`.
- **Sharing the server's Valkey.** Rejected: the console would reach the server's single-use state
  ([ADR-1437](1437-the-management-console-is-a-confidential-client-served-by-a-bff-host-of-its-own.md)).
- **The server's ServiceAccount.** Rejected: it may carry the server's cloud identities.
- **Routing `/api` through the console's BFF.** Rejected: machine callers authenticate at the OP directly.

## Consequences

- One release deploys both images at one version.
- An operator registers `mgmt-console` with `jwks_uri` `https://<host>/bff/jwks` and creates two key Secrets.
- The `Build console image` check runs on every pull request.

## References

- [ADR-1437](1437-the-management-console-is-a-confidential-client-served-by-a-bff-host-of-its-own.md),
  [ADR-1439](1439-a-bff-loads-the-key-rings-its-configuration-names-from-pem-files.md),
  [ADR-1178](1178-the-host-image-is-chiseled-non-root-multi-arch-and-carries-the-login-bundle.md),
  [ADR-1179](1179-the-host-chart-references-its-secrets-and-refuses-a-release-the-host-would-refuse.md),
  [ADR-1249](1249-the-host-images-base-images-are-pinned-by-digest-and-tracked-by-dependabot.md)
- `src/NCode.Identity.ManagementConsole.Host/Dockerfile`, `.github/workflows/console-image.yml`,
  `charts/ncode-identity-host`
- Issue [#610](https://github.com/NCodeGroup/Identity/issues/610)
