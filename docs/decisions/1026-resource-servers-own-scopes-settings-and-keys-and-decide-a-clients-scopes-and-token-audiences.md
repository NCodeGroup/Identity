# 1026. Resource servers own scopes, settings and keys, and decide a client's scopes and token audiences

- **Status:** Accepted
- **Date:** 2026-10-12
- **Deciders:** NCode Group

## Context

An operator must be able to define an API, declare its permissions, grant a client access to it, and have the runtime
enforce all of it. Three questions follow from that.

**Where a scope lives.** Two data-model archetypes exist. Scopes nested under an API (Auth0 "Resource Server", Okta
"Authorization Server") make a scope belong to exactly one API, the API is the audience, and a client is authorized to
an API with a subset of its scopes. A flat scope catalog (Duende `ApiScope` and `ApiResource`, OpenIddict `Scope`) makes
scopes top-level and reusable, and clients list allowed scope names. The same noun means different things in different
APIs: `Invoice.Read` on a billing API is not `Invoice.Read` on a reporting API. A scope's meaning is defined by its
API, so one API should own it.

**What decides a client's scopes.** A flat per-client or per-server `scopes_supported` list used as a ceiling would be a
second source of truth beside the resource-server and client-grant model: a grant could authorize a scope the list
omits, or the list could permit a scope no resource server defines. One of them has to be authoritative.

**What a token is for.** An access token's `aud` tells a resource server "this token is for you", and the resource
server validates a token by finding its own identifier there. Each scope is owned by exactly one resource server, so a
token's effective scopes already say which resource servers it targets. A client that holds scopes for several APIs
still needs a way to ask for a token aimed at one of them, which is what RFC 8707 resource indicators are for, and FAPI
deployments (issue [#54](https://github.com/NCodeGroup/identity/issues/54)) need tokens bound tightly to their audience.

A resource server also owns per-audience token material, like a client or the tenant does. An API may share a symmetric
signing secret with the provider, or own a JWE decryption key so that access tokens minted for its audience are readable
only by it. That material belongs to the resource-server boundary, so a resource server is a settings owner and a secret
owner as clients, tenants and the platform are.

## Decision

**A tenant-owned resource server owns its scopes, settings and secrets. Resource servers and client grants alone decide
which scopes a client may request. An access token's audience is the resource servers that own its effective scopes,
narrowed by any RFC 8707 `resource` the client names.** Auth0 vocabulary is used for familiarity.

### The model

- **Resource server** (`ResourceServerEntity`, Auth0 "API"): an opaque `ResourceServerId`
  ([ADR-1014](1014-management-resources-carry-opaque-server-identifiers-and-list-one-keyset-page-at-a-time.md)), an
  audience `Identifier` that is unique per tenant, a `Name`, `IsDisabled`, `IsSystem`, a settings document and secrets.
  The resource server, its settings and its secrets each have their own concurrency token
  ([ADR-1012](1012-concurrency-tokens-are-interceptor-managed-and-a-lost-race-surfaces-as-a-store-neutral-exception.md)).
- **Scope** (`ScopeEntity`, Auth0 "Permission"): owned by one resource server, with a `Value` unique there by normalized
  value, a `Description` and `IsSystem`. The same value on two resource servers is two scopes.
- **Client grant** (`ClientGrantEntity`, Auth0 "Client Grant"): the authorization of a client to a resource server, at
  `…/clients/{clientId}/grants/{resourceServerId}`. The granted scope values are a JSON array (`ScopesJson`), not a join
  table. A write is refused unless every value is one of the resource server's scopes.
- **System resource server:** every tenant is seeded (`OpenIdIdentityResourceServerSeedHandler`) with the system
  resource server `urn:ncode:openid`, which owns the system scopes `openid`, `profile`, `email`, `address`, `phone` and
  `offline_access`. It holds no keys ([ADR-1167](1167-the-server-validates-the-tokens-it-issued-under-one-policy.md)).
  The standard OpenID Connect scopes thus have one home, and everything is a scope under a resource server.
- **Tenant scoping:** the resource server, its scopes and client grants are materialized only within the ambient tenant
  ([ADR-1018](1018-tenant-selection-is-shared-and-tenant-bound-rows-are-scoped-at-the-persistence-layer.md)).
- **Deletion:** the management API answers `409` to deleting a system resource server or a system scope, and to deleting
  a resource server that a client grant references or a client is bound to.

### Settings and secrets

A resource server is a settings owner and a secret owner like a client, through `PersistedResourceServerSettings` and
`PersistedResourceServerSecrets`. `ResourceServerApiEndpointHandler` serves `…/settings`, `…/settings/limits`,
`…/effective-settings` and the secrets endpoints, with the one-time reveal of
[ADR-1050](1050-secret-material-revealed-once-on-create.md) and optimistic concurrency, and audits each change. The
endpoints sit under the scope families `resource_servers` and `resource_server_secrets`, and authorization flows through
the ownership and admin handlers against the `resource_server` node
([ADR-1417](1417-management-authority-is-a-role-assignment-at-a-node-of-the-platform-team-and-tenant-hierarchy.md)).
The settings document holds only settings that apply within `resource_server`
([ADR-1149](1149-settings-declare-the-scopes-they-apply-to-and-the-scopes-are-a-registered-cascade.md)), and the
effective settings merge it onto the tenant's effective settings (platform, team, tenant). Runtime use of the stored
material to protect a token is [ADR-1158](1158-an-access-token-is-bound-by-its-client-and-every-resource-server-it-targets.md)'s.

### A client's allowed scopes

The replaceable `IClientScopeService.GetAllowedScopesAsync` returns the set of scopes a client may request in a tenant.
It is the union, over the tenant's enabled resource servers, of:

- every scope of a system resource server, with no client grant required, so `openid`, `profile` and `offline_access`
  just work and any system resource server is implicitly available to every client; and
- for each other resource server, the client grant's scopes intersected at read time with the resource server's current
  scopes. A granted value that is not among the resource server's current scopes is ignored, so removing a scope narrows every grant
  at once and a stale value is inert.

The authorization and token request validators refuse a scope outside that set with `invalid_scope`, comparing scope
tokens case-sensitively (RFC 6749). No setting caps a client's scopes. Discovery's `scopes_supported` is the distinct
scopes of the enabled resource servers (`DefaultDiscoverScopesHandler`). `offline_access` is always available as a
scope, and a refresh token is issued only when the client requests `offline_access` and `refresh_token` is in
`grant_types_supported`.

`IClientScopeService` sets the ambient tenant explicitly for its reads, because the OpenID runtime establishes none.

### An access token's audience

An access token's `aud` is the identifiers of the enabled resource servers that own its effective scopes, resolved by
`IResourceServerCredentialResolver`. `IClientScopeService.ResolveAudiencesAsync` exposes the same mapping. The claim is a
string for one audience and an array for several, and it is the client id for a scopeless token, so a token is never
audience-less. An ID token's `aud` is always the client id, as OpenID Connect requires. A token carrying `openid` or
`profile` is audienced to the system resource server's identifier, its UserInfo audience, by the same rule.

### Resource indicators (RFC 8707)

The `resource` parameter (`OpenIdCommonParameters.Resources`, `OpenIdConstants.Parameters.Resource`) repeats on
authorization and token requests (`IAuthorizationRequest`, `ITokenRequest`). Its values match resource server
identifiers ordinally, and no absolute-URI syntax constraint applies because identifiers are the authority. It only
narrows the scope-derived audience: it never grants access to a resource the client holds no scope for. No discovery
metadata advertises it, since RFC 8707 and RFC 8414 define none.

Every refusal is `invalid_target` (`OpenIdConstants.ErrorCodes.InvalidTarget`, `IOpenIdErrorFactory.InvalidTarget`):

- at the authorization endpoint, a resource that is not an enabled resource server of the tenant
  (`GetEnabledResourceServerIdentifiersAsync`);
- at code redemption, a resource outside those the authorization authorized; and
- at `IResourceServerCredentialResolver.ResolveAsync`, a resource that does not resolve to an enabled resource server
  owning one of the token's effective scopes. Every access-token path funnels through `DefaultTokenService` and that
  resolver, so the authorization code, refresh, client credentials, password and device code grants all get the same
  narrowing and enforcement.

`CreateSecurityTokenRequest.Resources` carries the effective resources into `DefaultTokenService`, which threads them to
the resolver so the emitted `aud` is the narrowed set. Without a `resource`, the audience is every scope-owning resource
server. `IResourceServerStore.FindEnabledByScopesAsync` pushes both the scope filter and the identifier filter to the
database
([ADR-1080](1080-filtered-reads-are-intent-named-store-methods-and-growing-criteria-are-init-only-dtos.md)).

## Options considered

- **A flat scope catalog (Duende and OpenIddict).** Rejected: it fits reusable scopes but cannot say that one noun means
  different things in different APIs, which is the deciding requirement.
- **A normalized grant-scope join table.** Rejected: it adds a fourth entity and its own management for a property that
  write-time validation and read-time intersection already make safe. A JSON array is simpler and matches Auth0.
- **OpenID scopes outside a resource server, as a special side channel.** Rejected: it forks the model. A system
  resource server keeps one uniform shape and makes the standard scopes first-class, describable rows.
- **A global, server-owned scope catalog.** Rejected: scopes and APIs are a tenant concern, consistent with the
  tenant-owned management surface.
- **A `scopes_supported` setting as the ceiling.** Rejected: it duplicates the resource-server and client-grant model,
  drifts from it, and cannot express which client may call which API.
- **Client grants for the OpenID Connect scopes.** Rejected: it adds ceremony for the universal case and diverges from
  Auth0, where OpenID scopes are always available. The system resource server's implicit availability gives the same
  guarantee from data.
- **Validating granted scopes only at write time.** Rejected: a scope removed from an API would stay honored until every
  grant is rewritten. Intersecting at read time keeps the resource server authoritative.
- **`aud` equal to the client id.** Rejected: it conflates the party that requested the token with the party that must
  accept it, so a resource server cannot tell a token minted for it from any other token the client holds.
- **`aud` taken only from `resource`.** Rejected: it adds a second way to say what the scopes already say, and a client
  that omits the parameter would get an unbound token.
- **Resource servers and client grants as the single source, with `resource` narrowing the audience (chosen).** One
  authoritative model for what scopes exist, who may request them and whom a token is for, enforced at runtime and
  reflected in discovery.

## Consequences

- Operators define APIs, declare their scopes and grant clients access, with Auth0-familiar vocabulary. A scope's
  identity is the pair of its resource server and its value.
- A client obtains a scope only from a system resource server or a client grant, so seeding a workload means seeding
  resource servers and grants.
- A resource server validates a token by checking its `Identifier` against `aud`, which may be an array, as RFC 7519
  allows. A token issued for API A is not accepted by API B.
- A client can name a `resource` and obtain a narrowly audienced token, which serves least privilege and FAPI audience
  binding. Validation is uniform across grant types because it lives at the credential-resolver choke point.
- The four owner types (platform, tenant, client, resource server) share one settings and secret shape, one set of store
  methods, one endpoint pattern and one set of base helpers.
- Request validation and token minting each perform a tenant-scoped read of the resource-server catalog and the client's
  grants, a caching opportunity that the design leaves open.
- Whether a scopeless access token should be issued at all is a separate validation concern.

## References

- [ADR-1012](1012-concurrency-tokens-are-interceptor-managed-and-a-lost-race-surfaces-as-a-store-neutral-exception.md),
  [ADR-1014](1014-management-resources-carry-opaque-server-identifiers-and-list-one-keyset-page-at-a-time.md) and
  [ADR-1018](1018-tenant-selection-is-shared-and-tenant-bound-rows-are-scoped-at-the-persistence-layer.md): concurrency
  tokens, opaque identifiers and tenant scoping that the entities reuse.
- [ADR-1010](1010-settings-merge-as-ceilings-and-floors-unset-lists-restrict-nothing-and-a-secure-baseline-supplies-defaults.md)
  and [ADR-1149](1149-settings-declare-the-scopes-they-apply-to-and-the-scopes-are-a-registered-cascade.md): the
  settings regime and the `resource_server` setting scope.
- [ADR-1025](1025-the-family-is-greenfield-and-pre-release-tracks-its-public-api-and-models-auth0-parity-plus.md): the
  Auth0-parity-plus philosophy.
- [ADR-1050](1050-secret-material-revealed-once-on-create.md),
  [ADR-1417](1417-management-authority-is-a-role-assignment-at-a-node-of-the-platform-team-and-tenant-hierarchy.md),
  [ADR-1167](1167-the-server-validates-the-tokens-it-issued-under-one-policy.md),
  [ADR-1158](1158-an-access-token-is-bound-by-its-client-and-every-resource-server-it-targets.md) and
  [ADR-1080](1080-filtered-reads-are-intent-named-store-methods-and-growing-criteria-are-init-only-dtos.md): secret
  reveal, management authority, system resource server keys, runtime key use and the filtered read.
- [RFC 6749](https://datatracker.ietf.org/doc/html/rfc6749), [RFC 7519](https://datatracker.ietf.org/doc/html/rfc7519),
  [RFC 8414](https://datatracker.ietf.org/doc/html/rfc8414) and
  [RFC 8707](https://datatracker.ietf.org/doc/html/rfc8707); issue [#54](https://github.com/NCodeGroup/identity/issues/54).
- Code: `ResourceServerEntity`, `ScopeEntity`, `ClientGrantEntity`, `IResourceServerStore`, `IClientGrantStore`,
  `IClientScopeService`, `IResourceServerCredentialResolver`, `DefaultTokenService`, `DefaultDiscoverScopesHandler`,
  `ResourceServerApiEndpointHandler` and `OpenIdIdentityResourceServerSeedHandler`.
