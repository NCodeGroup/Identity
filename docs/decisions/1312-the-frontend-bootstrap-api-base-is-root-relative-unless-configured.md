# 1312. The frontend bootstrap API base is root-relative unless configured

- **Status:** Accepted
- **Date:** 2026-11-01
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0126](project-template/0126-published-urls-come-from-a-configured-origin.md)
- **Amends:** [ADR-1182](1182-the-frontend-bootstrap-issuer-is-the-serving-tenants-not-the-requests.md)

## Context

[ADR-1182](1182-the-frontend-bootstrap-issuer-is-the-serving-tenants-not-the-requests.md) takes the bootstrap `issuer`
from the serving tenant, and it kept the `apiBaseUrl` default as the request's `scheme://host`. The management calls
that follow carry the user's access token, so a forged or proxied `Host` could point them at another origin.
[project-template ADR-0126](project-template/0126-published-urls-come-from-a-configured-origin.md) says that the request
contributes nothing to a URL the server publishes.

## Decision

When `Frontend:ApiBaseUrl` is unset, the bootstrap document publishes the host's path base as a root-relative value:
`/` at the root, `/identity` under the `/identity` path base. The browser resolves it against the origin that served the
bundle, so the `Host` header contributes nothing. A same-origin link may stay relative
([project-template ADR-0126](project-template/0126-published-urls-come-from-a-configured-origin.md) decision 2).

- `Frontend:ApiBaseUrl` stays the absolute override for a split deployment.
- The response stays `Cache-Control: no-store`, because the issuer depends on the tenant that serves the surface.
- The standalone Vite dev servers proxy `/api/` to the OP host, next to `/config/bootstrap.json`
  (`defineStandaloneSurfaceConfig`), so the root-relative value reaches the API in local development.

## Options considered

- Require `Frontend:ApiBaseUrl` everywhere. Rejected: it is a second value that must agree with the host.
- Derive the API base from the tenant's issuer. Rejected: under `DynamicByHost` the console at `manage.<apex>` would call
  `auth.<apex>` cross-origin.
- A root-relative path base unless configured (chosen).

## Consequences

- A forged `Host` cannot change where the bundle sends its access token.
- A bundle served from a path base calls the API beneath that path base without configuration.
- `@ncode/config` keeps the value as it parses it: a trailing slash is removed, so `/` becomes the empty string and a
  call joins to `/api/...`.

## References

- [`FrontendBootstrapDocument.cs`](../../src/NCode.Identity.Server/Frontend/FrontendBootstrapDocument.cs),
  [`FrontendBootstrapEndpointProvider.cs`](../../src/NCode.Identity.Server/Frontend/FrontendBootstrapEndpointProvider.cs)
  and [`FrontendBootstrapOptions.cs`](../../src/NCode.Identity.Server/Frontend/FrontendBootstrapOptions.cs).
- [`preset.ts`](../../src/frontend/packages/vite-config/src/preset.ts): the standalone proxy.
- [NCodeGroup/Identity#442](https://github.com/NCodeGroup/Identity/issues/442).
