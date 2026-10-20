# 160. A tenant's address is validated by the strategy that serves it, wherever it is written

- **Status:** Accepted (extended by
  [ADR-0165](0165-every-served-tenant-has-an-issuer-and-its-strategy-supplies-the-default.md): the seam also derives a
  tenant's default issuer, a tenant a strategy serves needs an issuer, and the root tenant's address is reserved)
- **Date:** 2026-10-19
- **Deciders:** NCode Group

## Context

A tenant's **address** is its identifier and its issuer: together with the configured tenant strategy, they decide where
the tenant's endpoints and discovery document are served. OpenID Connect Discovery requires the issuer to be the URL the
discovery document is fetched beneath, and a configured issuer is the tenant's base address as-is
([ADR-0138](0138-protocol-endpoints-are-served-under-the-tenant-route.md)), so an issuer the strategy does not serve the
tenant beneath publishes a discovery document conforming clients reject.

Two paths write a tenant's address. Configuration supplies the static tenant's, and it is validated when the host starts
([ADR-0137](0137-configuration-binds-strictly-and-is-validated-at-startup.md)). The management API writes every other
tenant's, and a check that exists only for configuration lets the management API store addresses nothing is served
under: an issuer with a query or fragment, a path tenant `acme` whose issuer does not end in `/acme`, a host tenant with
no issuer at all. The host strategy routes by the host of a tenant's issuer, and that host is deliberately not unique in
the schema because path tenants share one; host uniqueness is a rule of the host strategy alone, to be enforced by
validation when it is in use ([ADR-0129](0129-the-tenant-issuer-is-the-single-authoritative-origin.md)). Without that
validation, two tenants on one host make every request to the host fail.

Where the rules live matters as much as whether they run. Startup validation that branches on the built-in strategy codes
closes the vocabulary of strategies ([ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md)): a custom
strategy could not contribute rules of its own, and a second copy of the rules in the management API would drift from
the first.

## Decision

**The tenant-selection seam judges a tenant's address, each strategy owns the rules for the routes it serves, and every
path that writes an address applies the same rules.**

1. **One seam.** `ITenantResolver.ValidateTenantAsync(tenantId, issuer, storeManager)` returns the reasons a tenant cannot
   be served, each a `TenantValidationFailure` that says whether it is an invalid value or a conflict with another
   tenant. The resolver validates the rules every strategy shares, then asks the selected
   `ITenantStrategy.ValidateTenantAsync` for its own:
   - **Shared:** the identifier is a lowercase host label
     ([ADR-0159](0159-a-tenant-is-named-by-its-creator.md)); the issuer is an absolute `https` URL — `http` only for a
     loopback host — with no user information, query, fragment, or whitespace, of at most 1000 characters (OpenID Connect
     Discovery §3, RFC 8414 §2); and no other tenant holds the issuer (a conflict).
   - **Every built-in strategy:** the issuer ends with the tenant route bound for the tenant; a path base the host is
     mounted under may precede it.
   - **Path-based:** the route is bound with the tenant's identifier, so tenant `acme`'s issuer ends in `/acme`. A tenant
     without an issuer remains valid; its base address is derived from the request that reaches it.
   - **Host-based:** the tenant needs an issuer, because only the host of its issuer reaches it; the configured host
     pattern maps that host to itself; and no other tenant holds that host (a conflict).
2. **Every write path.** The management API's tenant validator calls the seam when a tenant is created, and when an
   update changes its issuer, answering `400` for invalid values before `409` for conflicts. The management API still
   never resolves a protocol tenant ([ADR-0042](0042-management-api-route-driven-tenant-scope.md)); it asks the selection
   seam a pure question, so the rules have one source
   ([ADR-0017](0017-tenant-resolution-shared-abstraction-and-management-boundary.md)).
3. **Startup uses the same rules.** The options validators share one internal rule set with the strategies
   (`TenantAddressRules`); they cannot call the strategies themselves, which read the options being validated.
4. **Each strategy validates its own options.** A shared validator checks the strategy-independent members
   (`StrategyCode`, and `RootTenantId` as a tenant identifier); each built-in strategy registers its own options
   validator beside it, which skips when another strategy is selected. The path strategy's route must carry the tenant
   parameter as its only parameter, and as a plain one — no constraint, default, optional marker, or catch-all — so a
   tenant's route, and therefore the issuer it must end with, follows from its identifier alone.

## Options considered

- **Copy the rules into the management API.** Rejected: two copies drift, and the management API would have to know every
  strategy, including ones registered by other packages.
- **Keep branching on the strategy code.** Rejected: it closes an open vocabulary; a custom strategy could neither
  validate its options nor judge the tenants it serves.
- **Report failures as plain messages.** Rejected: a caller must distinguish a value that is invalid on its own (`400`)
  from one another tenant already holds (`409`).
- **A unique schema index on the issuer host.** Rejected for the reason in ADR-0129: path tenants legitimately share a
  host. The full issuer keeps its unique index.
- **Judge the address on every update.** Rejected: a tenant written under an earlier configuration could not have its
  display name changed. The address is judged when it changes, which is when it can become wrong.
- **Fail at request time.** Rejected: the failure would reach relying parties rather than the operator who wrote the
  address.

## Consequences

- A tenant written through the management API can only be given an address its strategy serves it under, and a host
  strategy can no longer hold two tenants on one host.
- `ITenantResolver` and `ITenantStrategy` gain `ValidateTenantAsync`; a custom strategy implements it, and validates its
  options by registering an `IValidateOptions<TenantResolutionOptions>` of its own.
- The static tenant's configured issuer may use `http` only for a loopback host, the configured root and static tenant
  identifiers must be valid tenant identifiers, and a path strategy's route may carry only the plain tenant parameter.
  Each stops the host when it starts.
- Host uniqueness is checked before the write rather than enforced by the schema, so two concurrent writes could both
  pass; management writes are rare and administrator-driven, and the full issuer remains guarded by its unique index.

## References

- [ADR-0129](0129-the-tenant-issuer-is-the-single-authoritative-origin.md) — the issuer and the host index.
- [ADR-0137](0137-configuration-binds-strictly-and-is-validated-at-startup.md) — configuration validated at startup.
- [ADR-0138](0138-protocol-endpoints-are-served-under-the-tenant-route.md) — the tenant route and the base address.
- [ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md) — open vocabularies.
- [ADR-0159](0159-a-tenant-is-named-by-its-creator.md) — the tenant identifier.
- Code: `NCode.Identity.OpenId.Tenants.ITenantResolver.ValidateTenantAsync`, `ITenantStrategy.ValidateTenantAsync`,
  `TenantValidationFailure`, `DefaultTenantResolver`, `TenantAddressRules`, `TenantStrategy`,
  `DynamicByPathTenantStrategy`, `DynamicByHostTenantStrategy`, `StaticSingleTenantOptionsValidator`,
  `DynamicByHostTenantOptionsValidator`, `DynamicByPathTenantOptionsValidator`,
  `NCode.Identity.OpenId.Management.Endpoints.Tenants.DefaultTenantValidator`.
