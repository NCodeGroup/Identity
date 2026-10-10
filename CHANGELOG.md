# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project aims to adhere to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html). Versioning is stamped in lockstep across the family by
Nerdbank.GitVersioning ([ADR-0003](docs/adr/0003-central-package-management-and-lockstep-versioning.md)); a breaking
change to the public API is a **major** version bump.

## [Unreleased]

### Added

- **Asymmetric secret public-key read endpoints.** A new read-only sub-resource,
  `GET /api/.../{servers|tenants|clients}/{ownerId}/secrets/{secretId}/public-key`, returns an asymmetric secret's
  **public** key material as a `PublicKeyResource` carrying both a JWK (RFC 7517, the same representation the JWKS
  endpoint publishes, reusing the registered `IJsonWebKeyConverter` pipeline) and a SubjectPublicKeyInfo PEM
  (`-----BEGIN PUBLIC KEY-----`) for copy-paste into an admin UI or crypto tooling. Private key material is never
  exposed; a symmetric secret has no public-key sub-resource and responds `404 Not Found`. The read is gated by the
  same `Operations.Read` authorization as reading the secret. See
  [ADR-0058](docs/adr/0058-asymmetric-public-key-material-read-path.md).
- **Local-account management API.** A new `local-accounts` resource family under
  `/api/tenants/{tenantId}/local-accounts` (mirroring the clients family) lets an operator create, list, get, update,
  delete, disable, enable, reset the credential of, and read/set the server-owned metadata of a local account, plus
  manage its owners. Account creation now eagerly provisions the owning `FederatedPrincipal` and its one-to-one
  self-issued `FederatedIdentity` in the same unit of work, so ownership and principal-level metadata (`ProfileMetadata`
  / `SystemMetadata`) have a stable target from the moment the account exists; the metadata endpoint is the
  administrator-gated write path that upholds the "`SystemMetadata` is never end-user-writable" invariant. The endpoints
  require the opt-in local-account persistence package and otherwise respond `501 Not Implemented`. Changes are audited
  via `LocalAccountChangedAuditEvent` and gated by `*:local_accounts` scopes. See
  [ADR-0057](docs/adr/0057-local-account-eager-provisioning-and-management-seam.md).
- New dependency-free **`NCode.Json`** package holding the shared `JsonElements` helper (`EmptyObject` /
  `OrEmptyObject` / `IsNullOrUndefined`, namespace `NCode.Json`), promoted out of `NCode.Identity.Abstractions` so the
  persistence layer (and any package beneath `NCode.Identity.*`) can reuse it without inheriting that package's ASP.NET
  Core framework reference. See [ADR-0055](docs/adr/0055-subject-metadata-is-a-principal-level-concept-resolved-at-issuance.md).
- Two new abstractions packages split out of `NCode.Identity.Abstractions`:
  **`NCode.Identity.AspNetCore.Abstractions`** (the ASP.NET Core HTTP surface — `UriDescriptor`, `HttpResultException`,
  and the `Results/*` contracts; it now carries the `Microsoft.AspNetCore.App` framework reference) and
  **`NCode.Identity.Settings.Abstractions`** (the `NCode.Identity.Settings.*` subsystem). Namespaces are unchanged, so
  the move is transparent to source. See
  [ADR-0056](docs/adr/0056-abstractions-split-into-framework-free-tiered-packages.md).

### Changed

- **`ILocalAccountProvisioner` is now the full local-account write seam.** `CreateAsync` takes the caller's
  `IStoreManager` and stages its writes (account, principal, and self-issued identity) without saving, and the seam
  gains `UpdateAsync`, `SetEnabledAsync`, and `ResetPasswordAsync` (plus `LocalAccountUpdateRequest` /
  `LocalAccountPasswordResetRequest`). `ILocalAccountStore` gains `GetPageAsync` and `RemoveAsync`;
  `IFederatedPrincipalStore` gains `UpdateAsync` and `RemoveAsync`; `IFederatedIdentityStore` gains `RemoveAsync`. See
  [ADR-0057](docs/adr/0057-local-account-eager-provisioning-and-management-seam.md).

- **`NCode.Identity.Abstractions` is now framework-free.** Its ASP.NET Core HTTP surface
  (`Models/UriDescriptor`, `Exceptions/HttpResultException`, `Results/*`) moved to the new
  `NCode.Identity.AspNetCore.Abstractions` package and its Settings subsystem moved to
  `NCode.Identity.Settings.Abstractions`, so the core drops the `Microsoft.AspNetCore.App` framework reference and is
  referenceable by any lower-tier package (including the JOSE/Secrets crypto branch). The JOSE
  `JsonElementExtensions.TryGetPropertyValue<T>` helper folded into `NCode.Json.JsonElements`, leaving a single
  universal `JsonElement` helper home, and detached JSON parsing standardizes on `JsonElement.Parse(string)`.
  `NCode.Identity.OpenId.Abstractions` now references `NCode.Registration.AspNetCore` directly (it exposes
  `ReadOnlyEndpointDisposition` on its public surface) rather than inheriting it transitively from the core. Namespaces
  are unchanged. See [ADR-0056](docs/adr/0056-abstractions-split-into-framework-free-tiered-packages.md).

- **Subject metadata (`ProfileMetadata` / `SystemMetadata`) is now a principal-level concept, resolved fresh at
  issuance.** The two bags moved off `LocalAccount` (and `PersistedLocalAccount` / `LocalAccountCreationRequest`) onto
  `FederatedPrincipal` / `PersistedFederatedPrincipal`, and are resolved at token/UserInfo issuance through a new
  pluggable `IPrincipalMetadataProvider` (default over `IFederatedPrincipalStore`) keyed by the resolved `PrincipalId`.
  They are therefore projected identically for every grant flow (ROPC, interactive, refresh) and every connection kind
  (local **or** external IdP), where before only ROPC-authenticated local accounts received them. The provider reads
  fresh per issuance (cluster-consistent); there is no built-in cache — host-side caching is a replaceable-provider
  concern that must be distributed. The ROPC subject builder no longer stamps metadata onto the subject. See
  [ADR-0055](docs/adr/0055-subject-metadata-is-a-principal-level-concept-resolved-at-issuance.md) (amends
  [ADR-0054](docs/adr/0054-local-account-metadata-projection.md)).

- Entity Framework persistence was split into a reusable **framework** package
  (`NCode.Identity.OpenId.Persistence.EntityFramework`) plus domain **slice** packages that plug into the shared,
  now-`DbSet`-less `OpenIdDbContext` via `IOpenIdModelContributor`. The core OpenID schema moved to
  **`NCode.Identity.OpenId.Persistence.EntityFramework.Core`** and the local-account schema moved from
  `NCode.Identity.OpenId.Accounts.EntityFramework` to **`NCode.Identity.OpenId.Persistence.EntityFramework.Accounts`**;
  each slice's root namespace matches its package id (`…EntityFramework.Core.*` / `…EntityFramework.Accounts.*`). The
  framework's store base (`BaseStore`, `BaseStoreWithResourceId`), the `AddStore` registration helper, and the
  `UseIdGenerator` attribute are now public so a slice reuses them instead of re-implementing the boilerplate. Hosts
  register the framework plus each slice (`AddEntityFrameworkCorePersistence<TDbContext>()`), and a slice may expose
  typed `DbSet` accessors for its own entities as extension members. See
  [ADR-0052](docs/adr/0052-entity-framework-persistence-is-a-framework-plus-slices.md).
- All persisted OpenID data is now **tenant-scoped except the server root**. `FederatedPrincipal`/`FederatedIdentity`
  (previously global) and the new `LocalAccount` are tenant-owned: their persisted DTOs gained a required `TenantId`,
  and their server-generated ids and natural keys are now unique **per tenant** rather than globally — the same human
  authenticating in two tenants is two principals. The tenant-scope global query filter is navigation-free: the
  `ISupportTenantEntity` contract now carries a denormalized `NormalizedTenantId` discriminator (its former `TenantId`
  foreign-key member was removed), so the filter is a single indexed-column comparison and the framework stays
  decoupled from the concrete tenant entity. See
  [ADR-0035](docs/adr/0035-federated-principals-and-identity-resolution.md) (amended) and
  [ADR-0018](docs/adr/0018-tenant-scoped-data-access-at-the-persistence-layer.md).

- The `NCode.Identity.OpenId.Playground` host adopted the first-party .NET 10 OpenAPI stack: `Swashbuckle.AspNetCore`
  was replaced by `Microsoft.AspNetCore.OpenApi` (`AddOpenApi()`/`MapOpenApi()`) for document generation and
  `Scalar.AspNetCore` (`MapScalarApiReference()`) for the UI. `Microsoft.OpenApi` was aligned to the 2.x line that the
  net10 OpenAPI stack targets, and the two OpenID endpoint operation builders (`DefaultAuthorizationEndpointHandler`,
  `DefaultTokenEndpointProvider`) now use the 2.x `OpenApiMediaType` model.
- The `NCode.Identity.OpenId.Playground` host migrated to the modern ASP.NET Core minimal-hosting model: the
  `Startup` class and the `Host.CreateDefaultBuilder`/`UseStartup` bootstrap were replaced by a single top-level
  `Program.cs` using `WebApplication.CreateBuilder(...)`. The integration tests are unaffected — they still boot
  through `WebApplicationFactory<PlaygroundApiMarker>`.
- The development-only signing-key opt-ins moved out of the `NCode.Identity.OpenId.Playground` host and into the
  `NCode.Identity.Server` composition root, so any host can consume them rather than each app re-implementing them
  (the Playground is just the EXE host that wires them in). `AddEphemeralDeveloperKeys()`,
  `AddDeveloperSigningKey(DirectoryInfo)`, and the `EphemeralOpenIdTenantFactory` /
  `DeveloperSigningKeyOpenIdTenantFactory` types now live under the `NCode.Identity.Server` namespace. See
  [ADR-0002](docs/adr/0002-ephemeral-development-keys.md) and
  [ADR-0021](docs/adr/0021-developer-signing-keys-seeded-through-persistence.md).
- The hierarchical endpoint-group mechanism (`IEndpointGroup`, `IEndpointGroupBuilder`, `IEndpointProvider`, the
  route-tree builder, and the endpoint/operation disposition types) moved out of `NCode.Identity.Abstractions` into a new
  cross-cutting package, **`NCode.Registration.AspNetCore`**, that layers on `NCode.Registration` and the ASP.NET Core
  shared framework. `IEndpointGroupBuilder` now **is** an `IServiceBuilder` (it inherits it) rather than exposing a bare
  `IServiceCollection`, so a group's `ConfigureServices` registers its endpoints, child groups, and the services those
  endpoints depend on — including `AddMessageFactory<T>()` — through one registration surface. The separate per-family
  `AddXxxEndpoint()` chain is gone: each endpoint family registers its own provider and services, and the group invokes
  each family once, so no endpoint is listed twice. Public types moved namespace
  (`NCode.Identity.Endpoints` → `NCode.Registration.AspNetCore`) and three symbols were renamed
  (`IIdentityEndpointRouteBuilder` → `IEndpointTreeRouteBuilder`, `MapIdentityEndpoints()` → `MapEndpointGroups()`, and
  the route-builder default implementation). See
  [ADR-0043](docs/adr/0043-endpoint-registration-is-a-cross-cutting-aspnetcore-package.md).
- Endpoint route-group composition is now declarative, hierarchical, and unified across the OpenID protocol and
  management surfaces. A named `IEndpointGroup` **owns its contents**: it exposes a `Name` and `Prefix`, an optional
  `ConfigureRoutes` that attaches shared route-group filters when routes are mapped, and a
  `ConfigureServices(IEndpointGroupBuilder)` that registers at startup the endpoints (`AddEndpoint<T>()`) and child
  groups (`AddGroup<T>()`) it contains, plus the services those endpoints depend on (via the builder's
  `ServiceCollection`). Registering a single root group (`AddEndpointGroup<TRoot>()`) walks that declaration and
  registers the whole subtree; the route builder materializes
  each group with its prefix and filters, maps the group's endpoints, and recurses into its child groups. The
  management surface composes a hierarchy — `api` (`/api`) → `api/tenant` (`/tenants/{tenantId}`) → `api/tenant/client`
  (`/clients/{clientId}`) — so tenant-bound families (clients, grants, resource servers, client grants) nest under the
  tenant and map only their relative sub-routes, and the protocol surface is a single `openid` group. This replaces the
  imperative `IEndpointGroupProvider` tier, the per-family marker interfaces, and the separate keyed
  `AddEndpointProvider<T>(groupName)` / `AddOpenIdEndpointProvider<T>()` registration calls, which were removed. See
  [ADR-0042](docs/adr/0042-management-api-route-driven-tenant-scope.md).
- The management `/api` surface derives its ambient tenant scope from the route's `{tenantId}` segment rather than the
  request's protocol-resolved tenant, so a central-admin caller can administer a tenant by id while the persistence-layer
  query filter confines the read fail-closed ([ADR-0018](docs/adr/0018-tenant-scoped-data-access-at-the-persistence-layer.md));
  absent a route `{tenantId}` (control-plane routes) it falls back to the resolved tenant, a no-op for the unscoped
  server/tenant rows those routes act on. The tenant effective-settings endpoint is consequently addressed by id —
  `GET /api/tenants/{tenantId}/effective-settings` (was `GET /api/tenant/effective-settings`, the request's current
  tenant) — reconstructing the view from the server baseline merged with the addressed tenant's persisted overrides.
  Nesting the remaining tenant-bound families (clients, grants, resource servers) under `/api/tenants/{tenantId}/…`
  follows. See [ADR-0042](docs/adr/0042-management-api-route-driven-tenant-scope.md).

### Added

- Authenticated **effective-settings** preview endpoints that return the resolved, merged settings view (server
  baseline + tenant/client overrides + descriptor defaults) as a flat `name → value` JSON object — using the same value
  representation as the discovery document but including settings that are not discoverable — for each settings-bearing
  entity: `GET /api/tenants/{tenantId}/effective-settings`, `GET /api/clients/{clientId}/
effective-settings`, and `GET /api/servers/{serverId}/effective-settings`. Each is gated by the same `Read`
  authorization as reading that entity's raw settings and carries no `ETag` (a merged view spans multiple persisted
  entities). This is the gated replacement for the removed anonymous discovery `showAll` override. New wire contracts
  `TenantEffectiveSettingsResource`, `ClientEffectiveSettingsResource`, and `ServerEffectiveSettingsResource`. See
  [ADR-0040](docs/adr/0040-effective-settings-preview-endpoints.md) (the tenant endpoint is addressed by id per
  [ADR-0042](docs/adr/0042-management-api-route-driven-tenant-scope.md)).
- End-to-end support for the OpenID Connect `claims` request parameter
  ([OIDC Core 5.5](https://openid.net/specs/openid-connect-core-1_0.html#ClaimsParameter)), now advertised as
  `claims_parameter_supported: true` in discovery. Requested `claims.id_token` claims are added on top of the
  scope-driven claims during ID-token issuance, and requested `claims.userinfo` claims are honored at the UserInfo
  endpoint: when an access token is issued for a request that carried `claims.userinfo`, the requested claims are
  persisted as a short-lived stateful grant (`OpenIdConstants.PersistedGrantTypes.UserInfoClaims`) keyed by the access
  token's `jti` and bounded by its lifetime, then resolved at UserInfo — the access token itself is never inflated.
  `value`/`values` are advisory and `essential` is best-effort; a host can impose stricter semantics with a
  higher-priority claims handler. See [ADR-0039](docs/adr/0039-claims-request-parameter.md).
- Data-driven resource ownership: persisted `(principal, role, resource-node)` role assignments
  (`PersistedRoleAssignment` / `IRoleAssignmentStore`), an additive `OwnershipHandler` that grants a principal every
  operation except `Create` on a resource it owns (or an ancestor node), and creator-as-owner on client, resource
  server, and tenant creation. Each of those resources gains owner-management endpoints
  (`GET`/`POST`/`DELETE /{resource}/{id}/owners`) returning the rich `OwnerResource`, with a one-owner invariant that
  refuses to orphan a resource (`409`). The ownership service and resource-node abstraction (`IResourceOwnershipService`,
  `IResourceNode`, `ResourceNode`, `ResourceNodeTypes`) are public so applications can own and enforce their own
  resource types. See [ADR-0034](docs/adr/0034-persisted-role-assignments-and-ownership.md).
- A single management resource server (`urn:ncode:management`) that gates the whole management API through granular
  per-leaf scopes composed as `{verb}:{family}` (for example `read:clients`, `update:tenant_settings`,
  `create:server_secrets`). Its control-plane scope families (`servers`, `server_settings`, `server_secrets`,
  `tenants`) are seeded only into the root (control-plane) tenant, while its tenant-plane families (a tenant's own
  settings and secrets, plus its clients, client secrets, resource servers, client grants, and grants) are seeded into
  every tenant. A `SystemResourceServerDescriptor` now declares a `Plane` (`SystemResourceServerPlane.Tenant` (default)
  or `Control`); the system-resource-server seeder is plane-aware and merges every provider that contributes to the same
  audience into one resource server per tenant, seeding `Control`-plane families only into the root tenant, whose
  identifier is `TenantResolutionOptions.RootTenantId` (default `"root"`). The static single-tenant strategy provisions
  the root tenant lazily alongside its workload tenant, so a single-tenant deployment gains its control plane with no
  extra configuration. See [ADR-0033](docs/adr/0033-authorization-model-scopes-roles-and-ownership.md) and
  [ADR-0031](docs/adr/0031-control-plane-management-resource-server-and-root-tenant-seeding.md).
- An `OpenID Connect` UserInfo endpoint (`GET`/`POST /oauth2/userinfo`,
  [OpenID Connect Core 5.3](https://openid.net/specs/openid-connect-core-1_0.html#UserInfo), auto-advertised as
  `userinfo_endpoint` in discovery). The endpoint is a thin shell over two mediator seams: a reusable
  `AuthenticateSubjectCommand` whose default handler authenticates the caller via the host's ASP.NET Core authentication
  scheme (the same mechanism the management API uses) and maps it to `SubjectAuthentication`; and a
  `GetUserInfoClaimsCommand` enricher pipeline whose default handler supplies only the required `sub` claim, with
  applications registering additional `ICommandHandler<GetUserInfoClaimsCommand>` handlers to contribute profile/email/
  other claims. The response is an extensible `[JsonExtensionData]` result (`UserInfoResult`). See
  [ADR-0030](docs/adr/0030-userinfo-endpoint-and-subject-authentication-seam.md).
- An `OAuth 2.0` token introspection endpoint (`POST /oauth2/introspect`,
  [RFC 7662](https://datatracker.ietf.org/doc/html/rfc7662), auto-advertised as `introspection_endpoint` in discovery).
  An authenticated client may probe a token's active state: the default handler validates a JWT access token against the
  tenant signing keys, issuer, and lifetime, otherwise resolves a refresh-token grant, and reports `{"active": false}`
  for anything else. The response is a discovery-style, extensible result (`IntrospectionResult` with
  `[JsonExtensionData]` claims) that additional `ICommandHandler<IntrospectTokenCommand>` handlers may enrich. The
  endpoint is a thin `IOpenIdEndpointProvider` that authenticates the client and parses a typed
  `ITokenIntrospectionRequest`, then delegates to the `IntrospectTokenCommand` mediator handler. See
  [ADR-0029](docs/adr/0029-token-introspection-endpoint.md).
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

- Setting merge is now a first-class policy expressed through a public `SettingMerge` vocabulary of lattice dual pairs
  (`Min`/`Max`, `And`/`Or`, `Intersect`/`Union`, `Keep`/`Replace`) in `NCode.Identity.Abstractions`, so host-authored
  descriptors reuse it. Each built-in setting is mapped to a **ceiling** (a parent caps the child), a **floor** (a
  parent sets an un-loosenable minimum), or an **override**, chosen by the setting's value polarity. A more-specific
  scope (tenant/client) can no longer loosen a broader scope's security-relevant setting: token/code lifetimes,
  `clock_skew`, and `subject_max_age` are ceilings a child may only shorten; `require_pkce`, the `*_encryption_required`
  flags, `claims_supported_is_strict`, `refresh_token_rotation_enabled`, and the `federated_identity_*` requirement
  flags are floors; `allow_*` and `request(_uri)_parameter_supported` are ceilings; `allowed_identity_providers` and
  `subject_types_supported` merge as intersect ceilings; and `tenant_issuer` is parent-owned (`Keep`).
  `allowed_identity_providers` is now fail-closed — unset means unrestricted, but an explicit (or narrowed-to-empty)
  allowlist denies every IdP. See [ADR-0038](docs/adr/0038-setting-merge-lattice-duals.md).
- Relocated the default `claims_supported` list from the descriptor literal into the replaceable
  `IDefaultSettingsProvider` baseline, so a host that enables `claims_supported_is_strict` can extend the allow-list
  instead of being frozen by the library's defaults; the advertised default is unchanged
  ([ADR-0010](docs/adr/0010-supported-settings-unset-means-unrestricted.md)).
- Probe the tenant cache before reading the tenant store on the resolution hot path. `ITenantResolver` and
  `ITenantStrategy` gain `TryGetTenantId(httpContext, out tenantId)`, a store-free identity probe satisfied by the
  static-single (configured id) and dynamic-by-path (route value) strategies; `DefaultOpenIdTenantFactory` uses it to
  serve a warm tenant with no tenant-store round-trip. Dynamic-by-host still performs a full resolve because mapping a
  domain to a tenant requires the store. A cache hit no longer re-reads the store, so a tenant disabled after it was
  cached serves until its cache entry expires. See
  [ADR-0037](docs/adr/0037-tenant-cache-probe-before-store-load.md).
- Renamed the authorization endpoint's subject pipeline to drop the redundant `Subject` noun (the namespace already
  supplies the context) now that a general subject-authentication seam exists: `AuthenticateSubjectCommand` →
  `AuthenticateCommand`, `AuthorizeSubjectCommand`/`AuthorizeSubjectDisposition` → `AuthorizeCommand`/
  `AuthorizeDisposition`, `ChallengeSubjectCommand` → `ChallengeCommand` (and the matching `Default*` handlers). The
  cross-slice `ValidateSubjectCommand`/`DefaultValidateSubjectHandler` were renamed to
  `ValidateSubjectAuthenticationCommand`/`DefaultValidateSubjectAuthenticationHandler` to reflect that they validate an
  existing login-session `SubjectAuthentication` (tenant/`max_age`/`acr`), not the generic authenticate pipeline. The
  root `Subject` capability keeps the `Subject` noun. See
  [ADR-0030](docs/adr/0030-userinfo-endpoint-and-subject-authentication-seam.md).
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

### Security

- Removed the undocumented `showAll` query-string override from the discovery endpoint
  (`/.well-known/openid-configuration`). It was unauthenticated and, when set, bypassed both the per-setting
  `IsDiscoverable` filter and the per-endpoint discoverability filter, letting any anonymous caller enumerate every
  non-advertised setting and endpoint. Discovery now always returns only the settings and endpoints explicitly marked
  discoverable. An authenticated management view of a tenant's effective settings is tracked as a separate follow-up.

### Fixed

- The unique index on a tenant's domain name is now a filtered index (`WHERE NormalizedDomainName IS NOT NULL`), so
  multiple tenants may omit the optional domain name without colliding on relational providers.

- Adopted repository-wide engineering conventions: Central Package Management, Nerdbank.GitVersioning lockstep
  versioning, SourceLink-to-GitHub provenance with portable symbols, a scripted Definition of Done, CSharpier
  formatting, and scoped coding-convention instruction files. See [`docs/adr/`](docs/adr/).

[Unreleased]: https://github.com/NCodeGroup/Identity/commits/dev
