# 23. Scopes and resources: an Auth0-style Resource Server owns its scopes; a Client Grant authorizes a client

- **Status:** Accepted
- **Date:** 2026-09-30
- **Deciders:** NCode Group

## Context

Scopes and API resources are not manageable. A scope is only a string in the `scopes_supported` setting, intersected
`server ∩ tenant ∩ client` ([ADR-0010](0010-supported-settings-unset-means-unrestricted.md)); there is no scope
identity, no per-scope metadata (description, the API it belongs to), and no API-resource/audience entity (the audience
is just the client id). Operators cannot define an API, declare its permissions, or grant a client access to it.

Two data-model archetypes exist in the industry:

- **Scopes nested under an API** (Auth0 "Resource Server", Okta "Authorization Server"): a scope belongs to exactly one
  API; the API is the audience; a client is authorized to an API with a subset of its scopes.
- **Flat scope catalog** (Duende `ApiScope` + `ApiResource`, OpenIddict `Scope`): scopes are top-level and reusable;
  resources reference them by name; clients list allowed scope names.

The same noun can mean different things in different APIs — `Invoice.Read` on a billing API is not `Invoice.Read` on a
reporting API. A scope's meaning is defined by its API, so a scope should be owned by exactly one API.

## Decision

**Adopt the Auth0 archetype: a tenant-owned Resource Server owns its scopes, and a Client Grant authorizes a client to
a Resource Server with a subset of that Resource Server's scopes.** Auth0 vocabulary is used for familiarity.

- **`ResourceServer`** (Auth0 "API") — tenant-owned, mirroring the client family: a surrogate `Id`, `TenantId`, an
  opaque-but-operator-meaningful `Identifier` (the audience) with a `NormalizedIdentifier` unique per tenant, a `Name`,
  `IsDisabled`, an open `SettingsJson` bag (signing algorithm, token lifetime, allow-offline-access, …), and an
  interceptor-managed `ConcurrencyToken` ([ADR-0012](0012-interceptor-managed-concurrency-tokens.md)).
- **`Scope`** (Auth0 "Permission") — a **composition owned by one Resource Server** (like a secret under a client): a
  `Value` with a `NormalizedValue` unique per Resource Server, a `Description`, and its own concurrency token. Because a
  scope belongs to its Resource Server, the same value on two Resource Servers is two distinct scopes.
- **`ClientGrant`** (Auth0 "Client Grant") — the client↔Resource-Server authorization, unique per
  `(ClientId, ResourceServerId)`. The granted scopes are stored as a **JSON array of scope values** (`ScopesJson`), not
  a normalized join. Data integrity is maintained without a join table by two guardrails (below).
- **Everything is tenant-scoped** ([ADR-0018](0018-tenant-scoped-data-access-at-the-persistence-layer.md)): the Resource
  Server, its scopes, and client grants are materialized only within the ambient tenant.
- **Standard OIDC scopes have a home.** A reserved **system Resource Server** (`IsSystem = true`) is seeded per tenant
  owning the standard identity scopes (`openid`, `profile`, `email`, `address`, `phone`, `offline_access`) as
  `IsSystem` scopes. System Resource Servers and system scopes cannot be deleted. This keeps a single uniform model —
  everything is a scope under a Resource Server — rather than a special identity-scope side channel.

### Integrity without a join table

`ClientGrant.ScopesJson` is a denormalized list, so a scope could in principle be deleted from its Resource Server
while a grant still references its value. Rather than a normalized `ClientGrantScope` join (a fourth table plus its own
management), integrity is kept at the two boundaries where it matters:

- **Write-time validation.** Creating or updating a Client Grant validates that every granted scope value exists on the
  target Resource Server; unknown scopes are rejected. No dangling reference is ever introduced.
- **Read-time intersection.** A client's effective scopes are `grant.Scopes ∩ resourceServer.currentScopes`, so a value
  left stale by a later scope deletion is simply ignored. A dangling reference is inert, never a bug.

The result is the simplicity of a JSON array with no cascade/cleanup obligation on scope deletion and no possibility of
corruption.

## Options considered

- **Flat scope catalog (Duende/OpenIddict archetype).** Rejected: a shared scope catalog fits "retire the
  `scopes_supported` setting" and reusable scopes, but it cannot express that the same noun means different things in
  different APIs — the deciding requirement. Owning a scope by its Resource Server is the correct semantics.
- **Normalized `ClientGrantScope` join for granted scopes.** Rejected for now: stricter referential integrity, but a
  fourth entity and its own management for a property that write-time validation plus read-time intersection already
  make safe. A JSON array is simpler and Auth0-faithful.
- **Standard OIDC scopes as a special side channel (not under a Resource Server).** Rejected: it forks the model. A
  reserved system Resource Server keeps one uniform shape and makes the standard scopes first-class, describable rows.
- **A global (server-owned) scope catalog.** Rejected: inconsistent with the tenant-owned management surface and the
  `Tenant → Client` ownership; scopes and APIs are a tenant concern.

## Consequences

- Operators can define APIs (Resource Servers), declare their scopes, and grant clients access — the missing capability,
  with Auth0-familiar vocabulary.
- A scope's identity is `(ResourceServer, Value)`, so the same noun can carry different meaning per API.
- Client Grants stay simple (a JSON array) yet safe, via write-time validation and read-time intersection.
- The standard OIDC scopes are seeded per tenant on a reserved system Resource Server and cannot be deleted.
- This ADR covers the model and its persistence and management surface. Making the catalog **authoritative at runtime**
  — pointing scope/audience validation and discovery's `scopes_supported` at Client Grants and Resource Server scopes,
  and retiring the client-level `scopes_supported` setting — is a deliberate, separate change (a later ADR), so the
  model ships as a purely additive foundation first.

## References

- [ADR-0010](0010-supported-settings-unset-means-unrestricted.md) — the `scopes_supported` setting this model will
  eventually make authoritative from the database.
- [ADR-0012](0012-interceptor-managed-concurrency-tokens.md) — interceptor-managed concurrency tokens reused by the new
  entities.
- [ADR-0014](0014-server-generated-opaque-resource-ids.md) — opaque server-generated identifiers reused for the new
  resources.
- [ADR-0018](0018-tenant-scoped-data-access-at-the-persistence-layer.md) — the tenant query filter the new families are
  scoped by.
- Code: `NCode.Identity.OpenId.Persistence.EntityFramework.Entities.ResourceServerEntity` / `ScopeEntity` /
  `ClientGrantEntity`; `NCode.Identity.OpenId.Persistence.Stores.IResourceServerStore` / `IClientGrantStore`.
