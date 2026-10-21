# 195. A tenant's mTLS endpoint alias is a routed tenant property

- **Status:** Accepted
- **Date:** 2026-10-21
- **Deciders:** NCode Group

## Context

RFC 8705 §5 lets an authorization server publish `mtls_endpoint_aliases`: copies of its endpoints on a second origin,
where the TLS terminator asks for a client certificate. [ADR-0097](0097-sender-constrained-access-tokens-dpop-and-mtls.md)
added this through the `mtls_endpoint_alias_authority` setting. Discovery rewrote the endpoints to that authority, but
nothing routed it. Under the host strategy, a tenant is found only by its issuer's host
([ADR-0129](0129-the-tenant-issuer-is-the-single-authoritative-origin.md)), so a request to a per-tenant alias host such
as `acme.mtls.example` found no tenant. A tenant could advertise endpoints that it could not serve.

A setting could not fix this. Routing needs an indexed lookup by host across tenants, before any tenant is known, and
the alias must be judged as a host: does it reach this tenant, and does any other tenant already serve it? That is the
same reasoning that made the issuer a tenant property rather than a setting (ADR-0129 §3). A merged, inherited alias is
also meaningless: a server-wide alias inherited by every tenant would route them all to one host.

## Decision

**A tenant's mTLS endpoint alias is a first-class tenant property. The host strategy routes its host to the tenant, and
the tenant's address is validated as two hosts.**

1. **The alias is a property of the tenant.** `PersistedTenant.MtlsEndpointAliasAuthority` is optional and holds an
   `https` origin (scheme, host, and optional port, with no path). `OpenIdTenant` exposes it, and discovery builds
   `mtls_endpoint_aliases` from it. The `mtls_endpoint_alias_authority` setting is removed, and a migration moves each
   stored value into the new column.
2. **The alias host is derived and indexed, never set on its own.** Its host is derived on write into
   `NormalizedMtlsEndpointAliasHost` beside `NormalizedIssuerHost`, as ADR-0129 does for the issuer, so the routed host
   cannot drift from the published alias.
3. **A host lookup checks both columns and fails closed.** `ITenantStore.GetOrDefaultByHostAsync` returns the one tenant
   whose issuer host or alias host matches. When more than one tenant matches, it returns nothing rather than picking
   one, so a conflict carried in by migrated data cannot route a request to the wrong tenant.
4. **The address is judged as two hosts.** The resolver rejects an alias that is not an `https` origin. The host
   strategy applies the same rules to the alias host as to the issuer host: the configured host pattern must map it back
   to the tenant, and no other tenant may serve it. The alias host must differ from the tenant's own issuer host,
   because one host cannot both ask for and skip a client certificate. The management API carries
   `mtlsEndpointAliasAuthority` on tenant create, update, and read, and an update re-judges the address when either
   value changes.

ADR-0129 still holds. The issuer remains the single authoritative origin that every published URL and token is anchored
to. The alias is a routed second origin for the endpoints only, and the published `issuer` never changes with the
request host.

## Options considered

- **Keep the setting and look it up at routing time.** Rejected: routing would have to scan every tenant's merged
  settings to find one host, and the setting merge lattice cannot express "this value belongs to exactly one tenant".
- **A shared alias host that routes by path.** Rejected: under the host strategy each tenant is one host, and the
  certificate prompt is per host. A shared host would need the path strategy's routing on top.
- **Throw on an ambiguous lookup.** Rejected: one bad row would turn every request to that host into a server error.
  Returning nothing serves "unknown tenant", and validation keeps new writes unique.

## Consequences

- A tenant served by the host strategy can publish and serve per-tenant mTLS aliases, which the reference deployment's
  `{tenant}.mtls…` hosts need.
- The alias is set per tenant through the management API. Not yet built (tracked in `BACKLOG.md`): a default alias
  derived from a template, in the way ADR-0165 derives the default issuer, and the address preview and console fields
  for it.
- Migrated aliases are not checked for uniqueness, because the migration cannot run the strategy. An ambiguous host
  serves no tenant until an operator fixes one of the tenants, and the next write to either tenant reports the conflict.

## References

- [RFC 8705 §5](https://www.rfc-editor.org/rfc/rfc8705#section-5) — mutual-TLS endpoint aliases.
- [ADR-0097](0097-sender-constrained-access-tokens-dpop-and-mtls.md) — sender-constrained access tokens.
- [ADR-0129](0129-the-tenant-issuer-is-the-single-authoritative-origin.md) — the tenant's issuer is the single
  authoritative origin.
- [ADR-0160](0160-a-tenants-address-is-validated-by-the-strategy-that-serves-it.md) — a tenant's address is validated by
  the strategy that serves it.
- [ADR-0165](0165-every-served-tenant-has-an-issuer-and-its-strategy-supplies-the-default.md) — every served tenant has
  an issuer, and its strategy supplies the default.
