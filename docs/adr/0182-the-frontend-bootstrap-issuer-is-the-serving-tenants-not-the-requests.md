# 182. The frontend bootstrap issuer is the serving tenant's, not the request's

- **Status:** Accepted
- **Date:** 2026-10-20
- **Deciders:** NCode Group

## Context

Every frontend surface reads its runtime coordinates from one host endpoint, `/config/bootstrap.json`
([ADR-0064](0064-frontend-presentation-platform.md)). The document names the `issuer` the bundle discovers its
authorization server from (`${issuer}/.well-known/openid-configuration`) and the `apiBaseUrl` its management calls go
to, carrying the user's access token. With neither configured, both defaulted to the serving request's
`scheme://host`.

Every served tenant's issuer is its stored, validated origin, and nothing else derives one from the request
([ADR-0165](0165-every-served-tenant-has-an-issuer-and-its-strategy-supplies-the-default.md)). The bootstrap document
was a second, unvalidated source of the same value, and it was wrong in two ways:

- **It trusted the `Host`.** A forged or proxied `Host` — or a shared cache that keyed one host's response under
  another — makes the bundle discover, and send its user to sign in at, an authority the operator never configured.
- **It named the wrong tenant.** Under `DynamicByHost` the Management console served at `manage.<apex>` signs in
  against the root tenant, whose issuer is `https://auth.<apex>`; the request origin is neither.

## Decision

The bootstrap `issuer` is the issuer of the tenant that serves the surface. `Frontend:Issuer` remains an explicit
override.

- **The serving tenant** is the tenant the request names without a store round trip
  (`ITenantResolver.TryGetTenantId`): under `StaticSingle` that is its one configured tenant. When the request names
  none — the dynamic strategies, which resolve a tenant only by reading the store — it is the root tenant
  (`TenantResolution:Root:TenantId`), the control plane that serves the Management console. The rule names no
  strategy, so a registered strategy is covered by what it already implements
  ([ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md)).
- **The issuer** is that tenant's stored issuer, which is what its discovery document publishes byte for byte. Before
  the tenant is provisioned it is the strategy's default (`ITenantResolver.GetDefaultIssuerAsync`), the value
  provisioning stores.
- **No issuer, no document.** When neither exists, the endpoint answers `500` with a problem that names the setting to
  configure. It never falls back to the request origin.

The `apiBaseUrl` still defaults to the serving origin, and `Frontend:ApiBaseUrl` remains its override. The management
API is mapped at `/api` on every host the server answers, so the origin that served the bundle is always an origin
that serves the API. Calling it there is same-origin and needs no CORS — production configures none for management —
and the origin is one the host admitted: production refuses to start with a wildcard `AllowedHosts`. No "management
origin" setting exists to default to, and adding one would make every deployment configure a second value that must
agree with the first. To keep a shared cache from serving one host's document for another, the response is
`Cache-Control: no-store`.

## Options considered

- **Resolve the serving tenant fully, as protocol requests do** (`ITenantResolver.ResolveAsync`). Rejected: under the
  dynamic strategies that resolves the protocol tenant the host or path names, so the console at `manage.<apex>`
  would get the tenant that `manage` happens to name, not the control plane it signs in against.
- **Switch on the strategy code** to pick the root or the static tenant. Rejected: a closed switch over an open
  vocabulary; the request-names-a-tenant test covers every strategy.
- **Keep the request origin as a last resort.** Rejected: it is exactly the unvalidated second source this decision
  removes. A tenant without an issuer is a configuration error, and it fails loudly.
- **Default the API base to a configured management origin.** Rejected for now: no such setting exists, the
  same-origin default is correct for every shipped topology, and `Frontend:ApiBaseUrl` covers a split deployment.

## Consequences

- The issuer a bundle trusts is the issuer its tokens carry, under every strategy, whatever `Host` the request
  carried.
- A bootstrap request reads the serving tenant from the store once. The document is fetched once per page load and is
  not cached.
- Under `DynamicByHost` and `DynamicByPath` every surface served from the host's root signs in against the root
  tenant. A deployment whose surfaces sign in against another tenant sets `Frontend:Issuer`.
- Which tenant authenticates the management API itself is a separate question, answered by
  [ADR-0191](0191-the-management-api-authenticates-against-the-control-plane-tenant.md): the control-plane tenant.

## References

- [`FrontendIssuerResolver`](../../src/NCode.Identity.Server/Frontend/FrontendIssuerResolver.cs) — the serving tenant
  and its issuer.
- [`FrontendBootstrapEndpointProvider`](../../src/NCode.Identity.Server/Frontend/FrontendBootstrapEndpointProvider.cs)
  — the override, the `500`, and `no-store`.
- [`FrontendBootstrapDocument`](../../src/NCode.Identity.Server/Frontend/FrontendBootstrapDocument.cs) — the issuer is
  never the request origin; the API base falls back to it.
- [ADR-0064](0064-frontend-presentation-platform.md),
  [ADR-0165](0165-every-served-tenant-has-an-issuer-and-its-strategy-supplies-the-default.md),
  [ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md).
