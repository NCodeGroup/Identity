# 165. Every served tenant has an issuer, and its strategy supplies the default

- **Status:** Accepted
- **Date:** 2026-10-19
- **Deciders:** NCode Group

## Context

A tenant's issuer is its identity: every token it mints asserts it, every relying party that trusts the tenant is
configured with it, and every absolute URL the server publishes for the tenant is built from it
([ADR-0129](0129-the-tenant-issuer-is-the-single-authoritative-origin.md),
[ADR-0138](0138-protocol-endpoints-are-served-under-the-tenant-route.md)). A tenant could still be stored without one.
Its origin was then taken from the request it was materialized for: the scheme, the `Host` header, the path base, and the
tenant route. That was a development convenience — one host answering under `localhost`, a container name, and a LAN
address at once — and the source of three problems:

- **It weakened a security control by default.** The request-derived origin lets a caller's `Host` header choose where
  the server tells clients to send their credentials. It was the default, not an opt-in, so every consumer of the library
  inherited it until it configured otherwise, and production code may not offer a way to weaken a control below its safe
  baseline (`csharp-production` §2, correct-by-default).
- **It kept two materialization paths.** The tenant factory derived a request-materialized tenant's base address from
  the request and an administrative materialization's from the issuer, and the tenant cache had to keep them apart.
- **A multi-tenant deployment could not avoid it.** A tenant created without an issuer — the natural shape when an
  operator names a tenant and expects it at the platform's canonical address
  ([ADR-0131](0131-hosted-platform-environment-model-and-hostname-scheme.md),
  [ADR-0159](0159-a-tenant-is-named-by-its-creator.md)) — had no address but the request's, and the host strategy could
  not serve it at all.

## Decision

**Nothing derives a tenant's issuer from the request. Every tenant a strategy serves has an issuer — the one written
with it, or the one its strategy derives for it — and that issuer is stored with the tenant.**

1. **A served tenant has an issuer.** The tenant factory takes a tenant's issuer and base address from its stored issuer
   alone, on every path, and fails closed for a stored tenant without one. Whether a tenant needs an issuer is a strategy
   rule: a strategy serves the tenants whose requests it routes, and each of those needs one. The static strategy serves
   only its configured tenant, so `StaticSingle:Issuer` is required and the root tenant beneath it — which it does not
   serve — has none; the dynamic strategies serve every tenant.
2. **The strategy supplies a default.** `ITenantStrategy.GetDefaultIssuerAsync` returns the issuer a tenant written
   without one is given, and `ITenantResolver.GetDefaultIssuerAsync` exposes it beside `ValidateTenantAsync`. The sources,
   in order: the root tenant's configured `Root:Issuer`; the static tenant's `StaticSingle:Issuer`; otherwise the
   expansion of the strategy's issuer template — `DynamicByHost:IssuerTemplate` (`https://{tenantId}.auth.example`) or
   `DynamicByPath:IssuerTemplate` (`https://op.example/{tenantId}`), an RFC 6570 level-1 template with the single
   variable `{tenantId}`. A tenant identifier is a lowercase host label, so it is substituted without escaping.
3. **Every write path derives, then judges.** Management create and update, and the provisioner that creates a
   configuration-defined tenant, apply `issuer ??= default` and then `ValidateTenantAsync`, which judges a derived issuer
   exactly like an explicit one ([ADR-0160](0160-a-tenants-address-is-validated-by-the-strategy-that-serves-it.md)). An
   update that clears the issuer restores the derived one.
4. **The derived issuer is stored.** It is the tenant's identity from the moment it is written, so changing a template
   never moves an existing tenant.
5. **Templates are validated when the host starts.** A template contains `{tenantId}` exactly once and no other
   expression, and its expansion for a probe identifier must be a valid issuer that its strategy serves: under the host
   strategy the identifier must vary the host, the host pattern must map that host to itself, and the issuer must end
   with the tenant path; under the path strategy the issuer must end with the route bound for the identifier. An issuer's
   host labels are at most 63 characters, and its host at most 253, so a template that embeds a long identifier in a
   longer label is rejected when that tenant is written.
6. **The root tenant's address is reserved for it.** The dynamic strategies create the root tenant when a request first
   reaches its address ([ADR-0166](0166-a-tenant-is-provisioned-through-one-pipeline-under-every-strategy.md)), so they
   require one at startup — `Root:Issuer` or the template — and no other tenant may take it.
7. **The address can be previewed.** `GET /api/tenants/{tenantId}/address[?issuer=]` returns the issuer the tenant has
   or would be given, whether it is derived, whether the tenant exists, and every reason it could not be served there,
   through the same seam that judges the write. It authorizes as a read when the tenant exists and as a create otherwise.
   The management console previews the address as the operator types, so a taken name or an unusable issuer is seen
   before the tenant exists — which matters once a deleted name cannot be reused (backlog `F70`).

## Options considered

- **Keep the fallback behind an explicit development opt-in**, refused by the production-safety gate. Rejected: a public
  switch that weakens a security control is what correct-by-default forbids, and it keeps both materialization paths. A
  development host pins its issuer like any other deployment; a host reached under several names publishes one of them.
- **Keep the fallback and refuse it only in Production.** Rejected: the production-safety gate reads configuration, and a
  tenant written without an issuer at runtime is not configuration, so the gate could not see it.
- **Derive the issuer at request time instead of storing it.** Rejected: an issuer is an identity relying parties
  configure. Re-deriving it would move every tenant whenever a template changed.
- **Configure a base address per strategy rather than a template.** Rejected for the host strategy, whose tenant
  identifier sits in the host; one template shape serves both built-in strategies and a custom one.
- **Preview the address at a collection route** (`/api/tenants/check-address`). Rejected: a literal segment under
  `/api/tenants/` collides with a tenant of that name. The sub-resource of the tenant it describes cannot collide, and it
  mirrors the effective-settings previews ([ADR-0040](0040-effective-settings-preview-endpoints.md)).

## Consequences

- A tenant's published URLs never follow the request. The spoofed-`Host` tests hold for every tenant, not only one with a
  pinned issuer.
- Every deployment configures an address: the static host its `StaticSingle:Issuer` (the reference host's launch
  profiles pin `https://localhost:5001` and their other ports), a dynamic host an issuer template or explicit issuers plus
  the root tenant's address. A development host is reached at the origin its issuer names.
- A tenant stored without an issuer before this decision fails closed; give it one through the management API, or reset
  a development database.
- Under the hosted platform, `DynamicByHost:IssuerTemplate = https://{tenantId}.auth.<apex>` and
  `Root:Issuer = https://auth.<apex>` produce the hostname scheme of ADR-0131, and a custom domain remains an explicit
  issuer.
- The frontend bootstrap document still defaults its own `issuer` and `apiBaseUrl` to the serving request's origin;
  deriving them from the tenant is tracked separately in the backlog.

## References

- [ADR-0129](0129-the-tenant-issuer-is-the-single-authoritative-origin.md) — the issuer as the single authoritative
  origin; this decision removes the request-derived fallback it kept.
- [ADR-0138](0138-protocol-endpoints-are-served-under-the-tenant-route.md) — its request-derived base address (decision
  3) is removed.
- [ADR-0160](0160-a-tenants-address-is-validated-by-the-strategy-that-serves-it.md) — the seam that judges an address now
  also derives its default.
- [ADR-0166](0166-a-tenant-is-provisioned-through-one-pipeline-under-every-strategy.md) — the root tenant created at its
  address.
- Code: `NCode.Identity.OpenId.Tenants.ITenantStrategy.GetDefaultIssuerAsync`, `ITenantResolver.GetDefaultIssuerAsync`,
  `RootTenantOptions`, `DynamicByHostTenantOptions.IssuerTemplate`, `DynamicByPathTenantOptions.IssuerTemplate`,
  `TenantAddressRules`, `DefaultOpenIdTenantFactory`,
  `NCode.Identity.OpenId.Management.Contracts.Tenants.TenantAddressResource`.
