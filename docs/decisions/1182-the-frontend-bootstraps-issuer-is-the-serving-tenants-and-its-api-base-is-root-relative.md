# 1182. The frontend bootstrap's issuer is the serving tenant's and its API base is root-relative

- **Status:** Accepted
- **Date:** 2026-11-01
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0126](project-template/0126-published-urls-come-from-a-configured-origin.md)

## Context

Every frontend surface reads its runtime coordinates from one host endpoint, `/config/bootstrap.json`
([ADR-1064](1064-one-frontend-platform-of-shared-packages-and-thin-app-shells-serves-every-web-surface.md)). The document names the `issuer` the bundle discovers its
authorization server from (`${issuer}/.well-known/openid-configuration`) and the `apiBaseUrl` its management calls go
to, carrying the user's access token.

Every served tenant's issuer is its stored, validated origin, and nothing else derives one from the request
([ADR-1165](1165-a-tenants-stored-issuer-is-its-single-origin-and-its-mtls-alias-a-routed-second-host.md)). A bootstrap
document that took either value from the serving request's `scheme://host` would be a second, unvalidated source of the
same value, and it would be wrong in two ways:

- **It would trust the `Host`.** A forged or proxied `Host`, or a shared cache that keyed one host's response under
  another, makes the bundle discover and send its user to sign in at an authority the operator never configured, or
  send the user's access token to another origin.
  [project-template ADR-0126](project-template/0126-published-urls-come-from-a-configured-origin.md) says the request
  contributes nothing to a URL the server publishes.
- **It would name the wrong tenant.** Under `DynamicByHost` a surface served from a host such as `manage.<apex>` signs
  in against the root tenant, whose issuer is `https://auth.<apex>`; the request origin is neither.

## Decision

The bootstrap document's `issuer` is the issuer of the tenant that serves the surface, and its `apiBaseUrl` is the
host's path base, root-relative. The request contributes nothing to either.

`FrontendBootstrapEndpointProvider` serves `/config/bootstrap.json` as a `FrontendBootstrapDocument`
(`NCode.Identity.Server/Frontend`), publishing `issuer`, `apiBaseUrl`, `clientId`, `scopes`, `features` and `branding`,
with per-surface overrides from `Frontend:Surfaces`.

### The issuer

- **The serving tenant.** `FrontendIssuerResolver` takes the tenant that the request names without a store round trip
  (`ITenantResolver.TryGetTenantId`): under `StaticSingle` that is the one configured tenant. When the request names
  none, it takes the root tenant (`TenantResolution:Root:TenantId`), the control plane. The rule names no strategy, so a
  registered strategy is covered by what it already implements
  ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)).
- **The issuer value** is that tenant's stored issuer, which its discovery document publishes byte for byte. Before the
  tenant is provisioned it is the strategy's default (`ITenantResolver.GetDefaultIssuerAsync`), the value provisioning
  stores.
- **`Frontend:Issuer`** is an explicit override.
- **No issuer, no document.** When neither exists, the endpoint answers `500` with a problem that names the setting to
  configure. It never falls back to the request origin.

### The API base

- Unset, `apiBaseUrl` is the host's path base as a root-relative value: `/` at the root, `/identity` under an
  `/identity` path base. The browser resolves it against the origin that served the bundle, so the `Host` header
  contributes nothing. A same-origin link may stay relative
  ([project-template ADR-0126](project-template/0126-published-urls-come-from-a-configured-origin.md) decision 2).
- `Frontend:ApiBaseUrl`, when set, is an absolute override for a split deployment.
- `@ncode/config` strips the trailing slash, so `/` becomes the empty string and calls join to `/api/...`.

### Caching and local development

- The response is `Cache-Control: no-store`, because the issuer depends on the serving tenant.
- A standalone Vite dev server (`defineStandaloneSurfaceConfig`) proxies only `/config/bootstrap.json` to the OP host.

## Options considered

- **Resolve the serving tenant fully, as protocol requests do** (`ITenantResolver.ResolveAsync`). Rejected: under the
  dynamic strategies that resolves the protocol tenant the host or path names, so a surface at `manage.<apex>` would get
  the tenant that `manage` happens to name, not the control plane it signs in against.
- **Switch on the strategy code** to pick the root or the static tenant. Rejected: a closed switch over an open
  vocabulary; the request-names-a-tenant test covers every strategy.
- **Keep the request origin as a last resort for the issuer.** Rejected: it is the unvalidated second source this
  decision excludes. A tenant without an issuer is a configuration error, and it fails loudly.
- **Require `Frontend:ApiBaseUrl` everywhere.** Rejected: it is a second value that must agree with the host.
- **Derive the API base from the tenant's issuer.** Rejected: under `DynamicByHost` the surface at `manage.<apex>` would
  call `auth.<apex>` cross-origin.
- **A configured management-origin setting.** Rejected: every deployment would configure a second value that must agree
  with the first, and `Frontend:ApiBaseUrl` covers a split deployment.
- **The serving tenant's issuer and a root-relative API base unless configured** (chosen).

## Consequences

- The issuer a bundle trusts is the issuer its tokens carry, under every strategy, whatever `Host` the request carried.
- A forged `Host` cannot change where the bundle sends its access token.
- A bundle served from a path base calls the API beneath that path base without configuration.
- A bootstrap request reads the serving tenant from the store at most once. The document is fetched once per page load.
- Under `DynamicByHost` and `DynamicByPath` a surface served from the host root signs in against the root tenant. A
  deployment whose surfaces sign in elsewhere sets `Frontend:Issuer`.
- Which tenant authenticates the management API is
  [ADR-1191](1191-a-management-request-is-authenticated-by-the-control-plane-tenant-and-scoped-by-its-routes-tenant.md)'s
  decision, and the management console reads its own BFF bootstrap document
  ([ADR-1438](1438-the-management-console-holds-no-token-and-reads-its-session-from-its-bff.md)).

## References

- [`FrontendIssuerResolver`](../../src/NCode.Identity.Server/Frontend/FrontendIssuerResolver.cs): the serving tenant and
  its issuer.
- [`FrontendBootstrapEndpointProvider`](../../src/NCode.Identity.Server/Frontend/FrontendBootstrapEndpointProvider.cs):
  the override, the `500` and `no-store`.
- [`FrontendBootstrapDocument`](../../src/NCode.Identity.Server/Frontend/FrontendBootstrapDocument.cs) and
  [`FrontendBootstrapOptions`](../../src/NCode.Identity.Server/Frontend/FrontendBootstrapOptions.cs): the document and
  its settings.
- [`preset.ts`](../../frontend/packages/vite-config/src/preset.ts): the standalone proxy.
- [NCodeGroup/identity#442](https://github.com/NCodeGroup/identity/issues/442).
- [ADR-1064](1064-one-frontend-platform-of-shared-packages-and-thin-app-shells-serves-every-web-surface.md),
  [ADR-1165](1165-a-tenants-stored-issuer-is-its-single-origin-and-its-mtls-alias-a-routed-second-host.md),
  [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md).
