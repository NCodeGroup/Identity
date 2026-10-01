# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project aims to adhere to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html). Versioning is stamped in lockstep across the family by
Nerdbank.GitVersioning ([ADR-0003](docs/adr/0003-central-package-management-and-lockstep-versioning.md)); a breaking
change to the public API is a **major** version bump.

## [Unreleased]

### Added

- An `OAuth 2.0` token revocation endpoint (`POST /oauth2/revoke`,
  [RFC 7009](https://datatracker.ietf.org/doc/html/rfc7009), auto-advertised as `revocation_endpoint` in discovery). An
  authenticated client may revoke one of its own refresh tokens; an unknown or non-revocable token (including a stateless
  JWT access token) is a no-op, and the endpoint always responds `200 OK` so it never reveals token state. The endpoint
  is a thin `IOpenIdEndpointProvider` that authenticates the client and parses a typed `ITokenRevocationRequest`, then
  delegates to the `RevokeTokenCommand` mediator handler. See
  [ADR-0028](docs/adr/0028-token-revocation-refresh-tokens-only.md).
- Shared tenant-resolution abstraction (`ITenantResolver` / `ITenantStrategy` / `TenantResolutionOptions` in
  `NCode.Identity.OpenId.Abstractions`, default implementation in `NCode.Identity.OpenId.Core`) so the OpenID runtime
  and the management API select the ambient tenant from a single source of truth. See
  [ADR-0018](docs/adr/0018-tenant-scoped-data-access-at-the-persistence-layer.md).
- Tenant-scoped data access for the management API: an `IAmbientTenantAccessor`
  (`NCode.Identity.OpenId.Persistence.Abstractions`) plus an `OpenIdDbContext` global query filter on tenant-child
  entities so that, when tenancy is request-derived (dynamic-by-host / dynamic-by-path), a resource outside the
  request's tenant is never materialized (a cross-tenant read returns `404`, closing the fetch-then-reject leak
  window). A reusable `TenantScopeEndpointFilter` establishes the scope; tenant and server administration remain
  central-admin (unscoped). See
  [ADR-0018](docs/adr/0018-tenant-scoped-data-access-at-the-persistence-layer.md).
- Collection `GET` endpoints for the management API — `GET api/clients`, `GET api/servers`, `GET api/tenants` — that
  return a page of resources (`CollectionResource<T>` = `{ items, continuationToken }`) using keyset (cursor)
  pagination (`?limit=` clamped to 100, `?cursor=` opaque continuation token). The client list is scoped to the
  request's tenant; the server and tenant lists are central-admin. There is deliberately no query protocol (no OData);
  see [ADR-0019](docs/adr/0019-collection-endpoints-keyset-pagination.md).
- Grant administration for the management API — a tenant-scoped, read-only surface: `GET api/grants` (paged, with
  optional `?subjectId=` / `?clientId=` filters), `GET api/grants/{grantId}`, `DELETE api/grants/{grantId}` (single
  soft-revoke), and `DELETE api/grants?subjectId=&clientId=` (bulk soft-revoke of every active match, requires at least
  one filter, returns `GrantRevocationResult { revoked }`). Revocation sets `RevokedWhen` and keeps the row for audit;
  it is idempotent. Grants are addressed by a new opaque, server-generated `GrantId` (`PersistedGrant.GrantId` /
  `GrantEntity.GrantId`), and `GrantResource` projects metadata only — the internal `HashedKey` and the grant
  `PayloadJson` are never exposed. The shared keyset-pagination mechanics move from `BaseStoreWithResourceId` to
  `BaseStore` so a grant store not keyed by a resource id can paginate on its chronological surrogate id. See
  [ADR-0020](docs/adr/0020-grant-management-read-surface.md) and
  [ADR-0022](docs/adr/0022-grant-filters-and-bulk-revocation.md).
- Scope and resource management (Auth0-aligned): a tenant-owned **resource server** (an API/audience) owns its
  **scopes** (permissions), and a **client grant** authorizes a client to a resource server with a subset of its
  scopes. Adds the persistence model (`ResourceServerEntity` / `ScopeEntity` / `ClientGrantEntity`, DTOs,
  `IResourceServerStore` / `IClientGrantStore`), seeds a reserved OpenID Connect system resource server
  (`urn:ncode:openid`) with the standard scopes into each tenant via `ISystemResourceServerProvider`, and the
  management surface `GET/POST/GET/PATCH/DELETE api/resource-servers` plus the `/{id}/scopes` sub-resource, and the
  nested `GET/POST/GET/PUT/DELETE api/clients/{clientId}/grants[/{resourceServerId}]` client-grant surface. Granted
  scopes are validated (write-time) against the target resource server's scopes. Core preconditions for both surfaces
  are asserted up-front by replaceable validators (`IResourceServerValidator` / `IClientGrantValidator`), consistent
  with the existing client/server/tenant validators. A reserved tenant-plane management resource server
  (`urn:ncode:management`) is also seeded into each tenant, whose `{verb}:{family}` scopes (for example `read:clients`,
  `create:resource_servers`, `delete:grants`) gate the per-tenant management families; control-plane families
  (servers, tenants) are intentionally excluded and belong to the root tenant's control plane. System
  resource servers and scopes cannot be deleted. See [ADR-0023](docs/adr/0023-scopes-and-resources-management-model.md)
  and [ADR-0024](docs/adr/0024-control-plane-and-per-tenant-planes.md).
- A persistent developer-keys opt-in for the Playground, `AddDeveloperSigningKey(...)`, that seeds an `RSA` signing key
  through the tenant secret store (so the runtime, the JWKS endpoint, and the management API share one source of truth)
  instead of overriding the read path. Paired with a local SQLite database and a persistent Data Protection key ring
  (DPAPI-protected on Windows), the key survives restarts and self-heals if the key ring is lost; the existing
  `AddEphemeralDeveloperKeys()` stays the hermetic path for tests. Selected via `DeveloperKeys:Mode=PersistentSigningKey`
  (set in `launchSettings.json`). See [ADR-0021](docs/adr/0021-developer-signing-keys-seeded-through-persistence.md).

### Changed

- Collapsed the per-strategy OpenID tenant providers, their selector, and the thin tenant factory into a single
  `DefaultOpenIdTenantFactory` that composes `ITenantResolver` for selection and performs only tenant materialization.
  Tenant-selection option types moved from `NCode.Identity.OpenId.Authentication` to
  `NCode.Identity.OpenId.Abstractions` (`NCode.Identity.OpenId.Tenants` namespace); `OpenIdTenantOptions` now carries
  only materialization settings.
- A managed client is always owned by the request's tenant, so `POST api/clients` no longer accepts a tenant
  identifier: `CreateClientRequest.TenantId` is removed and the server derives the owning tenant from the request
  (resolved for every tenant strategy, static-single included). This removes the redundant, error-prone echo of a
  tenant the server already resolved.
- The management API endpoints are now served under a shared top-level `/api` route group (`/api/clients`,
  `/api/servers`, `/api/tenants`, …) via a `ManagementEndpointGroupProvider` ([ADR-0009](docs/adr/0009-endpoint-families-own-their-route-group.md)),
  keeping them distinct from the OpenID protocol endpoints at the root.
- Grants are now strictly tenant-scoped: the previously nullable `TenantId` on the grant vertical slice
  (`PersistedGrantId`, `PersistedGrant`, `PersistedGrant<TPayload>`, `GrantEntity`, `GrantResource`) and the
  `IPersistedGrantService.CreateGrantId` parameter are now required (non-nullable). Every grant is created against the
  resolved request tenant, so the unused non-tenant (global) key space is removed; a future server-level grant type, if
  needed, would be modeled explicitly rather than by an absent tenant.
- Runtime scope enforcement is now driven by the resource-server / client-grant model instead of a `scopes_supported`
  setting, which is retired (its `SettingKey`, descriptor, and default are removed). A replaceable `IClientScopeService`
  resolves a client's allowed scopes — every scope of a system resource server (`IsSystem`) is implicitly available to
  all clients, while a non-system API is available only through a client grant intersected with the API's current
  scopes (read-time intersection) — and the authorization/token validators reject out-of-scope requests. Discovery's
  `scopes_supported` is derived from the tenant's resource-server catalog. `offline_access` availability follows from the
  system identity resource server; refresh-token issuance remains governed by the request and the `refresh_token` grant
  type. See [ADR-0026](docs/adr/0026-scope-enforcement-via-resource-servers-and-client-grants.md).
- An access token's `aud` is now bound to the resource servers that own its effective scopes (resolved via
  `IClientScopeService.ResolveAudiencesAsync`) instead of the requesting client, so a resource server can validate that a
  token was minted for it; a token spanning several resource servers carries a multi-valued `aud`, and a scopeless token
  falls back to the client id. The ID token's `aud` remains the client. See
  [ADR-0027](docs/adr/0027-access-token-audience-from-resource-servers.md).

### Fixed

- The unique index on a tenant's domain name is now a filtered index (`WHERE NormalizedDomainName IS NOT NULL`), so
  multiple tenants may omit the optional domain name without colliding on relational providers.

- Adopted repository-wide engineering conventions: Central Package Management, Nerdbank.GitVersioning lockstep
  versioning, SourceLink-to-GitHub provenance with portable symbols, a scripted Definition of Done, CSharpier
  formatting, and scoped coding-convention instruction files. See [`docs/adr/`](docs/adr/).

[Unreleased]: https://github.com/NCodeGroup/Identity/commits/dev
