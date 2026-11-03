# 1195. A tenant's mTLS endpoint alias is a routed tenant property

- **Status:** Accepted (amended by
  [ADR-1251](1251-a-retired-setting-stays-registered-until-a-later-release-removes-it.md): the setting is retired and
  its value copied; amended by
  [ADR-1394](1394-until-the-first-stable-release-each-host-database-context-has-one-initial-migration.md):
  until the first stable release each host database context has one re-scaffolded initial migration, and no
  migration rewrites stored data; amended by
  [ADR-1396](1396-until-the-first-stable-release-a-setting-is-renamed-removed-or-narrowed-outright.md), which
  supersedes ADR-1251: until the first stable release a setting is renamed, removed or narrowed outright, and no
  retired descriptor stays registered)
- **Date:** 2026-10-21
- **Deciders:** NCode Group

## Context

RFC 8705 §5 lets an authorization server publish `mtls_endpoint_aliases`: copies of its endpoints on a second origin,
where the TLS terminator asks for a client certificate. [ADR-1097](1097-sender-constrained-access-tokens-dpop-and-mtls.md)
added this through the `mtls_endpoint_alias_authority` setting. Discovery rewrote the endpoints to that authority, but
nothing routed it. Under the host strategy, a tenant is found only by its issuer's host
([ADR-1129](1129-the-tenant-issuer-is-the-single-authoritative-origin.md)), so a request to a per-tenant alias host such
as `acme.mtls.example` found no tenant. A tenant could advertise endpoints that it could not serve.

A setting could not fix this. Routing needs an indexed lookup by host across tenants, before any tenant is known, and
the alias must be judged as a host: does it reach this tenant, and does any other tenant already serve it? That is the
same reasoning that made the issuer a tenant property rather than a setting (ADR-1129 §3). A merged, inherited alias is
also meaningless: a server-wide alias inherited by every tenant would route them all to one host.

## Decision

**A tenant's mTLS endpoint alias is a first-class tenant property. The host strategy routes its host to the tenant, and
the tenant's address is validated as two hosts.**

1. **The alias is a property of the tenant.** `PersistedTenant.MtlsEndpointAliasAuthority` is optional and holds an
   `https` origin (scheme, host, and optional port, with no path). `OpenIdTenant` exposes it, and discovery builds
   `mtls_endpoint_aliases` from it. The `mtls_endpoint_alias_authority` setting is removed, and a migration moves each
   stored value into the new column.
2. **The alias host is derived and indexed, never set on its own.** Its host is derived on write into
   `NormalizedMtlsEndpointAliasHost` beside `NormalizedIssuerHost`, as ADR-1129 does for the issuer, so the routed host
   cannot drift from the published alias.
3. **A host lookup checks both columns and fails closed.** `ITenantStore.GetOrDefaultByHostAsync` returns the one tenant
   whose issuer host or alias host matches. When more than one tenant matches, it returns nothing rather than picking
   one, so a conflict carried in by migrated data cannot route a request to the wrong tenant. Validation does not route:
   it uses `ListTenantIdsByHostAsync`, which returns every holder, so a host that is already ambiguous still counts as
   taken.
4. **The address is judged as two hosts.** The resolver rejects an alias that is not an `https` origin. The host
   strategy applies the same rules to the alias host as to the issuer host: the configured host pattern must map it back
   to the tenant, and no other tenant may serve it. The alias host must differ from the tenant's own issuer host,
   because one host cannot both ask for and skip a client certificate. The management API carries
   `mtlsEndpointAliasAuthority` on tenant create, update, and read, and an update re-judges the address when either
   value changes.
5. **The strategy supplies a default alias, as it does the issuer.**
   `ITenantStrategy.GetDefaultMtlsEndpointAliasAuthorityAsync` returns the alias a tenant written without one is given,
   and `ITenantResolver` exposes it beside `GetDefaultIssuerAsync`
   ([ADR-1165](1165-every-served-tenant-has-an-issuer-and-its-strategy-supplies-the-default.md)).
   The sources, in order: the root tenant's configured `Root:MtlsEndpointAliasAuthority`; the static tenant's
   `StaticSingle:MtlsEndpointAliasAuthority`; otherwise the expansion of `DynamicByHost:MtlsEndpointAliasTemplate`
   (`https://{tenantId}.mtls.example`). No other strategy has a template, so its tenants have no default alias.
   Management create and update, and the provisioner, apply `alias ??= default` and then judge the address, and the
   derived alias is stored so it does not move when configuration changes. An update that clears the alias restores the
   derived one, as an update that clears the issuer does. The template is checked at startup: it must be an `https`
   origin template with `{tenantId}` in its host, the host pattern must map each expanded host to itself, and it must
   give a tenant a host other than its `DynamicByHost:IssuerTemplate` host; a configured root alias must map to itself
   too. The address preview
   (`GET /tenants/{tenantId}/address`) takes an optional `mtlsEndpointAliasAuthority` and returns the effective
   `mtlsEndpointAliasAuthority` and whether it is derived (`isMtlsEndpointAliasAuthorityDerived`).

ADR-1129 still holds. The issuer remains the single authoritative origin that every published URL and token is anchored
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
- Under the hosted platform, `DynamicByHost:MtlsEndpointAliasTemplate = https://{tenantId}.mtls.<apex>` and
  `Root:MtlsEndpointAliasAuthority = https://mtls.<apex>` give every tenant its alias without an API call; an explicit
  alias still overrides the derived one. Under a template, a tenant cannot opt out of an alias: an empty alias restores
  the derived one. The management console edits the alias beside the issuer and previews its derived default.
- Migrated aliases are not checked for uniqueness, because the migration cannot run the strategy. An ambiguous host
  serves no tenant until an operator fixes one of the tenants, and the next write to either tenant reports the conflict.

## References

- [RFC 8705 §5](https://www.rfc-editor.org/rfc/rfc8705#section-5) — mutual-TLS endpoint aliases.
- [ADR-1097](1097-sender-constrained-access-tokens-dpop-and-mtls.md) — sender-constrained access tokens.
- [ADR-1129](1129-the-tenant-issuer-is-the-single-authoritative-origin.md) — the tenant's issuer is the single
  authoritative origin.
- [ADR-1160](1160-a-tenants-address-is-validated-by-the-strategy-that-serves-it.md) — a tenant's address is validated by
  the strategy that serves it.
- [ADR-1165](1165-every-served-tenant-has-an-issuer-and-its-strategy-supplies-the-default.md) — every served tenant has
  an issuer, and its strategy supplies the default.
