# 0066. Resource servers are first-class settings and secret owners

- **Status:** Accepted
- **Date:** 2026-10-11
- **Deciders:** NCode.Identity maintainers

## Context

Clients, tenants, and the server are each **settings owners** (a persisted JSON settings object with its
own concurrency token, edited through a dedicated `.../settings` sub-resource and previewed as merged
effective settings) and **secret owners** (a nested secret collection under `.../secrets` honoring the
one-time reveal of [ADR-0050](0050-secret-material-revealed-once-on-create.md)). Resource servers (APIs,
in Auth0 terms) were the exception: `PersistedResourceServer` carried `Settings` as a bare `JsonElement`
with no independent concurrency token and no management endpoints, and had **no** secret collection at
all. The management console (backlog `U2`) extended the settings/secrets surface to tenants and the
server but had to leave resource servers out because the backend owner did not exist.

The open question ([`BACKLOG.md`](../../BACKLOG.md) `U2`) was whether an API should be a **secret** owner
at all. Auth0 APIs do not authenticate with a client secret, which argued for "settings-only". But that
framing is too narrow: in a comprehensive OpenID Provider a resource server owns **per-audience
access-token material** —

- an **HS256** API shares a symmetric **signing secret** with the OP (the OP signs, the API validates);
- an API may own a **JWE decryption key** so access tokens minted for its audience are readable only by
  it.

That material belongs to the resource-server / audience boundary, exactly as a client's belongs to the
client boundary and the server's/tenant's signing keys belong to theirs. "An API is not a _client-auth_
secret owner" is true but does not imply "an API owns no secrets". The cohesive model is that **every**
leaf that can hold key material is a secret owner, addressed uniformly.

## Decision

Make resource servers first-class **settings and secret owners**, structurally identical to clients:

- `PersistedResourceServer` gains a wrapped `PersistedResourceServerSettings` (its own concurrency
  token) in place of the bare `JsonElement`, and a `PersistedResourceServerSecrets` collection. A new
  `BasePersistedResourceServerResource` mirrors the client/tenant/server resource bases, and the
  `resource_server` resource type plus an `ISupportResourceServerId` abstraction are added.
- `IResourceServerStore` gains `UpdateSettingsAsync` and the secret CRUD (`GetSecretsOrDefaultAsync`,
  `GetSecretOrDefaultAsync`, `AddSecretAsync`, `UpdateSecretAsync`, `RemoveSecretAsync`). The EF model
  adds `SettingsConcurrencyToken`/`SecretsConcurrencyToken` columns and a tenant-scoped
  `ResourceServerSecrets` join table (migration `AddResourceServerSecrets`). Deleting a resource server
  cascades its scopes **and** secrets, matching its self-cleaning scope behavior.
- `ResourceServerApiEndpointHandler` gains the settings endpoints (`GET`/`PATCH .../settings`,
  `GET .../effective-settings` merging server → tenant → resource server) and the full secrets endpoints
  (`GET`/`POST`/`GET {id}`/`GET {id}/public-key`/`PUT {id}`/`DELETE {id}`), reusing the shared
  `BaseApiEndpointHandler` helpers, the one-time reveal (ADR-0050), and optimistic concurrency.
- **Settings move out of the core resource contract.** `ResourceServerResource` and
  `UpdateResourceServerRequest` no longer carry `Settings`; create still seeds initial settings via
  `CreateResourceServerRequest`. This matches how clients already model it and the management console's
  existing resource-server view.
- Scope-families: a `resource_server_secrets` family is seeded; resource-server **settings** fold into
  the `resource_servers` family, following the client leaf model (see
  [ADR-0033](0033-authorization-model-scopes-roles-and-ownership.md)). Authorization of the new endpoints
  flows through the existing ownership/admin handlers against the `resource_server` resource node and the
  settings/secret objects — no new authorization code.

### Scope boundary

This ADR delivers **management** (CRUD) parity only. **Runtime consumption** of resource-server secrets —
actually signing/encrypting access tokens per audience with the stored material — is a separate feature;
resource servers have no runtime domain object today and are not yet resolved at token-issuance time.
Likewise, resource-server management operations are **not audited** yet: the resource-server handler emits
no audit events for any operation (create/update/delete/scopes included), so auditing is deferred as a
single uniform follow-up rather than bolted onto only the new leaves. Both are tracked in
[`BACKLOG.md`](../../BACKLOG.md).

## Consequences

- The four owner types (server, tenant, client, resource server) are now uniform: one settings/secret
  shape, one set of store methods, one endpoint pattern, one set of base helpers. New owners cost less.
- Greenfield breaking changes land cleanly ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)):
  `PersistedResourceServer.Settings` changed type, `ResourceServerResource`/`UpdateResourceServerRequest`
  dropped `Settings`, and the store interface grew — all acceptable pre-release, with `PublicAPI`
  churn expected.
- A new EF migration (`AddResourceServerSecrets`) adds two columns and the `ResourceServerSecrets` table.
- The HS256-signing-secret storage question (hash-at-rest vs reversible) is **not** resolved here; it
  rides on the existing `S5` fork and applies equally to every symmetric secret owner.
