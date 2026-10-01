# 26. Scope enforcement is driven by resource servers and client grants, not a `scopes_supported` setting

- **Status:** Accepted
- **Date:** 2026-09-30
- **Deciders:** NCode Group

## Context

A request's scopes must be validated at the authorization and token endpoints: a client may only obtain the scopes it
is entitled to. The data model already defines a tenant's **resource servers** (APIs) that own their **scopes**, and a
**client grant** that authorizes a client to a resource server with a subset of its scopes
([ADR-0023](0023-scopes-and-resources-management-model.md)). That model is the authoritative description of what a client
may request — but it is inert unless the runtime consults it.

A separate, older mechanism also described "allowed scopes": a per-client/server `scopes_supported` setting (a flat
list) used as a ceiling in request validation and advertised in discovery. Keeping both is two sources of truth for one
concept: a grant could authorize a scope the setting omits, or the setting could permit a scope no resource server
defines. One of them has to be authoritative.

## Decision

**The resource-server / client-grant model is the single source of truth for a client's allowed scopes; the
`scopes_supported` setting is retired.** A replaceable `IClientScopeService` resolves the set of scopes a client may
request in a tenant, and the authorization- and token-request validators reject any requested scope outside that set
(`invalid_scope`).

The allowed set for a client is computed as the union, over the tenant's enabled resource servers, of:

- **every scope of a system resource server** (`IsSystem`) — a system resource server is **implicitly available to
  every client**, with no client grant required (this is how the OpenID Connect identity resource server makes
  `openid`/`profile`/… and `offline_access` "just work," generalized to any system resource server per
  [ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)); and
- **the granted scopes of a non-system resource server, intersected with that resource server's current scopes** — a
  client grant authorizes a non-system API, and a granted scope no longer defined on the resource server is silently
  ignored (**read-time intersection**), so revoking a scope on the API immediately narrows every grant without a
  migration.

Scope tokens are compared case-sensitively (RFC 6749). Discovery's `scopes_supported` is **derived** from the distinct
scopes of the tenant's enabled resource servers rather than echoed from a setting.

Because `offline_access` is a scope on the system identity resource server, it is always _available_; whether a refresh
token is actually _issued_ remains governed by the client requesting `offline_access` and by the `refresh_token` grant
type being enabled (`grant_types_supported`) — so the retired setting's secondary "prohibit refresh tokens" role is
subsumed, not lost.

## Options considered

- **Keep `scopes_supported` as the ceiling.** Rejected: it duplicates the resource-server/client-grant model, drifts
  from it, and cannot express per-API authorization (which client may call which API) that grants provide.
- **Require an explicit client grant even for OpenID Connect scopes.** Rejected: it adds ceremony for the universal
  case and diverges from the familiar Auth0 behavior where OIDC scopes are always available; the `IsSystem` implicit
  grant delivers the same guarantee data-driven.
- **Validate granted scopes only at write time (no read-time intersection).** Rejected: a scope removed from an API
  would linger in grants and keep being honored until every grant is rewritten; intersecting at read time makes the
  resource server authoritative at all times.
- **Resource-server / client-grant model as the single source of truth (chosen).** One authoritative model for both
  "what scopes exist" and "who may request them," enforced at runtime and reflected in discovery.

## Consequences

- Request validation performs a tenant-scoped read of the resource-server catalog and the client's grants. The OIDC
  runtime does not establish an ambient tenant scope, so `IClientScopeService` sets it explicitly for those reads
  ([ADR-0018](0018-tenant-scoped-data-access-at-the-persistence-layer.md)); this per-request read is a caching
  opportunity (for example on the materialized tenant) left as future work.
- A client can no longer obtain a scope merely because a flat setting listed it; it must come from a system resource
  server or a client grant. Seeding a workload therefore means seeding resource servers and grants, not widening a
  setting.
- `scopes_supported` is removed as a setting (key, descriptor, default) while remaining a discovery **metadata field
  name**, now populated from the catalog.
- Which resource servers are _advertised_ in discovery (for example, whether to list management scopes) is currently
  "all enabled resource servers"; narrowing that is a later refinement.

## References

- [ADR-0023](0023-scopes-and-resources-management-model.md) — the resource server / scope / client grant model this
  decision enforces at runtime.
- [ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md) — the Auth0-parity-plus philosophy and the
  `IsSystem`-implicit-grant generalization.
- [ADR-0018](0018-tenant-scoped-data-access-at-the-persistence-layer.md) — the ambient-tenant scoping the resolver sets
  explicitly at the OIDC runtime.
- [ADR-0010](0010-supported-settings-unset-means-unrestricted.md) — the `*_supported` settings regime that
  `scopes_supported` leaves.
