# 1160. A tenant is named by its creator, validated by its strategy, and never reissued

- **Status:** Accepted
- **Date:** 2026-10-20
- **Deciders:** NCode Group
- **Supersedes:** [project-template ADR-0123](project-template/0123-resource-ids-are-server-generated-and-opaque.md)

## Context

Resource identifiers are server-generated and opaque ([ADR-1014](1014-server-generated-opaque-resource-ids.md)), and
project-template ADR-0123 asks the same of every resource: an identifier is a meaningless anchor for foreign keys and
audit records, and a caller never chooses one. A tenant's identifier is different, because it is not internal.

- **It is part of the tenant's issuer.** Under path-based tenancy the identifier is the tenant route's segment, and a
  configured issuer is the tenant's base address as-is ([ADR-1165](1165-a-tenants-stored-issuer-is-its-single-origin-and-its-mtls-alias-a-routed-second-host.md)),
  so tenant `acme` is served beneath, and must publish, an issuer ending in `/acme`. The issuer is asserted in every
  token the tenant mints and configured by every relying party that trusts it, so it is effectively immutable once
  clients integrate ([ADR-1165](1165-a-tenants-stored-issuer-is-its-single-origin-and-its-mtls-alias-a-routed-second-host.md)).
- **It is a name people use.** The management API addresses a tenant by it
  ([ADR-1042](1042-management-api-route-driven-tenant-scope.md)), and the hosted platform names a tenant by the leftmost
  label of its host ([ADR-1495](1495-the-hosted-platform-runs-customer-environments-as-tenants-on-production-under-the-ncodeid-net-platform-domain.md)).
- **A server-minted identifier cannot be validated against its issuer.** It exists only inside the create call, so a
  caller cannot know the issuer a path tenant must have until the tenant exists.

A tenant's address is its identifier and its issuer; together with the configured tenant strategy they decide where the
tenant's endpoints and discovery document are served. OpenID Connect Discovery requires the issuer to be the URL the
discovery document is fetched beneath, so an issuer the strategy does not serve the tenant beneath publishes a document
that conforming clients reject.

Two paths write an address. Configuration supplies the static tenant's, validated when the host starts
([ADR-1137](1137-configuration-binds-strictly-and-every-option-timer-period-and-retention-is-validated-at-startup.md)).
The management API writes every other tenant's, and a check that exists only for configuration lets it store addresses
nothing is served under: an issuer with a query or fragment, a path tenant `acme` whose issuer does not end in `/acme`,
a host tenant with no issuer. The host strategy routes by the host of a tenant's issuer, and that host is deliberately
not unique in the schema because path tenants share one, so host uniqueness is a rule of the host strategy alone. Where
the rules live matters as much as whether they run: startup validation that branches on the built-in strategy codes
closes the vocabulary of strategies ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)), and a second
copy of the rules in the management API would drift from the first.

Last, relying parties key their trust by the issuer string, not by a database row. If deleting a tenant freed its name
and issuer, a new tenant could take `acme` and an issuer that relying parties had configured to trust the old tenant,
and they would accept its tokens. The server must remember an identity after the tenant that held it is gone. Auth0
names a tenant by its creator, forbids renaming it and never reuses a deleted name.

## Decision

**A tenant's identifier is a name its creator chooses and never changes, the tenant-selection seam judges its address
through the strategy that serves it on every write path, and a retired identifier or issuer is never reissued.**

### The identifier

1. **The creator chooses it.** `POST /api/teams/{teamId}/tenants` carries `tenantId`; the server mints none.
   `UpdateTenantRequest` does not carry it, so no update changes it, and `DisplayName` is the editable label. Clients,
   resource servers, secrets and principals keep server-generated opaque identifiers.
2. **It is a lowercase RFC 1123 host label.** `TenantAddressRules.IsValidTenantId` accepts 1 to 63 lowercase letters,
   digits or hyphens that start and end with a letter or digit. That one form is valid as a path segment and as the
   leftmost label of a host, so the name works under every built-in strategy, and a path tenant's issuer
   (`https://op.example/acme`) is known before the tenant exists: a tenant is created with its address in one request.
3. **It is validated wherever it enters:** the management API through the seam below, and the configured identifiers
   (`TenantResolution:Root:TenantId`, `TenantResolution:StaticSingle:TenantId`) when the host starts.

### One seam judges the address

4. `ITenantResolver.ValidateTenantAsync(tenantId, issuer, mtlsEndpointAliasAuthority, storeManager, …)` returns the
   reasons a tenant cannot be served, each a `TenantValidationFailure` that is an invalid value or a conflict with
   another tenant. The resolver checks the shared rules, then asks the selected `ITenantStrategy.ValidateTenantAsync`
   for its own.
5. **Shared rules:**
   - the identifier is valid;
   - the issuer is an absolute `https` URL (`http` only for a loopback host) with no user information, query, fragment
     or whitespace, of at most 1000 characters (OpenID Connect Discovery section 3, RFC 8414 section 2);
   - no other tenant holds the issuer (a conflict);
   - the identifier is not retired;
   - the issuer is not retired, unless this tenant gave it up.
6. **Strategy rules.** Every built-in strategy requires the issuer to end with the tenant route bound for the tenant; a
   path base the host is mounted under may precede it. The path route carries the tenant parameter as its only
   parameter, and a plain one (no constraint, default, optional marker or catch-all), so the route, and the issuer it
   must end with, follows from the identifier alone. The host strategy requires the configured host pattern to map the
   issuer's host to itself and no other tenant to hold that host (a conflict).
7. **Every write path uses the seam.**
   - The management validator calls it when a tenant is created and when an update changes the address, answering `400`
     for invalid values before `409` for conflicts. It asks the selection seam a pure question and never resolves a
     protocol tenant ([ADR-1042](1042-management-api-route-driven-tenant-scope.md)).
   - Startup options validators share the internal `TenantAddressRules`, because they cannot call the strategies, which
     read the options being validated.
   - A shared validator checks `StrategyCode` and `TenantResolution:Root:TenantId`. Each built-in strategy registers its
     own `IValidateOptions<TenantResolutionOptions>` that skips when another strategy is selected, so a custom strategy
     validates its own options the same way.
8. **The address preview.** `GET /api/tenants/{tenantId}/address` and
   `GET /api/teams/{teamId}/tenants/{tenantId}/address` (optional `issuer`, `mtlsEndpointAliasAuthority`) return a
   `TenantAddressResource` (each value, whether it is derived, `exists` and `failures`) through the same seam. They
   authorize as a read when the tenant exists and as a create otherwise; the console previews as the operator types.

### Retired identities

9. **The store keeps tombstones.** `RetiredTenants` holds a deleted tenant's identifier and `RetiredIssuers` an issuer a
   tenant gave up, with the identifier of the tenant that held it; each has a unique normalized column. `RetiredTeams`
   retires a deleted team's identifier. `TenantStore.RemoveAsync` retires the tenant's identifier and issuer, and
   `UpdateAsync` retires a previous issuer whose normalized form changes (including when it is cleared; a change of
   letter case only retires nothing), in the same unit of work, so no store caller bypasses it.
10. **Lookups.** `ITenantStore.IsTenantIdRetiredAsync` and `GetRetiredIssuerHolderOrDefaultAsync` expose them. Only the
    tenant that gave up an issuer takes it back, and the retired row stays.
11. **A required tenant cannot be deleted.** `ITenantStrategy.IsRequiredTenant`, which `ITenantResolver` delegates to,
    names the tenants the deployment needs (the root tenant, the static strategy's tenant), and the management API
    answers `409` to deleting one, before it checks dependents. A custom strategy names its own.

## Options considered

- **Opaque identifiers with a path tenant's issuer derived from a configured base address (the Microsoft Entra shape).**
  Rejected: issuers become unreadable, operators cannot address a tenant by a name they know, and the hosted platform's
  tenant hostnames are names anyway.
- **An opaque identifier plus a unique, caller-chosen slug (the Keycloak realm shape).** Rejected: a second identity to
  keep unique and in step with the first, a store lookup on every path-routed request, and the opaque identifier buys a
  tenant nothing since its issuer is already an immutable public identity.
- **Mixed case or a wider character set.** Rejected: not a valid host label, and with case-insensitive store identity
  two spellings would name one tenant.
- **A reserved-name list such as `api`.** Rejected: routing matches whole paths, so a tenant named `api` collides only
  with an endpoint registered at exactly one of its protocol paths, and a closed list would have to be maintained
  forever.
- **Create without an issuer, then set it.** Rejected: the tenant exists for a while with no address, and every client
  needs two calls for one intent.
- **Copy the rules into the management API, or branch on the strategy codes.** Rejected: two copies drift, the API
  would have to know every strategy, and it closes an open vocabulary
  ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)).
- **Report failures as plain messages.** Rejected: a caller must distinguish a value invalid on its own (`400`) from one
  another tenant holds (`409`).
- **A unique schema index on the issuer host.** Rejected: path tenants legitimately share a host. The full issuer keeps
  its unique index.
- **Judge the address on every update.** Rejected: a tenant written under an earlier configuration could not have its
  display name changed. The address is judged when it changes.
- **One tombstone table, or retiring in the management endpoint.** Rejected: an issuer row needs its former holder and
  an identifier row does not, and a seeder, importer or consumer code that deletes through `ITenantStore` would bypass
  an endpoint.
- **An API to release a retired issuer.** Rejected: a release is the one action that lets a relying party accept a
  stranger's tokens. An operator who must release an issuer deletes its `RetiredIssuers` row in the database, outside
  the product.
- **Chosen:** a creator-chosen immutable name, one validation seam owned by the strategies, and store-level tombstones.

## Consequences

- A tenant cannot be renamed, and a deleted name or issuer is never reused; an operator who deletes a tenant by mistake
  creates it under a new identifier. The editable label is `DisplayName`.
- A tenant written through the management API can only be given an address its strategy serves it under, and a host
  strategy cannot hold two tenants on one host.
- A custom strategy implements `ValidateTenantAsync` and `IsRequiredTenant`, and registers its own options validator.
- The static tenant's configured issuer may use `http` only for a loopback host, the configured root and static
  identifiers must be valid tenant identifiers, and a path route may carry only the plain tenant parameter; each stops
  the host at start.
- Host uniqueness is a pre-write check two concurrent writes could both pass; management writes are rare and
  administrator-driven, and the full issuer keeps its unique index.
- Every tenant validation reads `RetiredTenants`, and one with an issuer may read `RetiredIssuers`: single-row lookups on
  a unique index. The tombstones hold no secrets and are never pruned.

## References

- [ADR-1014](1014-server-generated-opaque-resource-ids.md) - opaque identifiers for every other resource.
- [ADR-1165](1165-a-tenants-stored-issuer-is-its-single-origin-and-its-mtls-alias-a-routed-second-host.md) - the issuer as the tenant's identity.
- [ADR-1165](1165-a-tenants-stored-issuer-is-its-single-origin-and-its-mtls-alias-a-routed-second-host.md) - the tenant route and the base address.
- [ADR-1042](1042-management-api-route-driven-tenant-scope.md) - the management API's tenant scope.
- [ADR-1137](1137-configuration-binds-strictly-and-every-option-timer-period-and-retention-is-validated-at-startup.md) -
  configuration validated at startup.
- [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md) - open vocabularies.
- [ADR-1495](1495-the-hosted-platform-runs-customer-environments-as-tenants-on-production-under-the-ncodeid-net-platform-domain.md) -
  tenant hostnames.
- [project-template ADR-0123](project-template/0123-resource-ids-are-server-generated-and-opaque.md) - server-generated
  identifiers.
- Code: `NCode.Identity.OpenId.Management.Contracts.Tenants.CreateTenantRequest.TenantId`,
  `NCode.Identity.OpenId.Tenants.ITenantResolver.ValidateTenantAsync`, `ITenantStrategy.ValidateTenantAsync`,
  `ITenantStrategy.IsRequiredTenant`, `TenantValidationFailure`, `DefaultTenantResolver`, `TenantAddressRules`,
  `DynamicByPathTenantStrategy`, `DynamicByHostTenantStrategy`, `DefaultTenantValidator`, `TenantStore`,
  `RetiredTenantEntity`, `RetiredIssuerEntity`.
