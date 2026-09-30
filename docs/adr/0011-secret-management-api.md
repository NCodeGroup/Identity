# 0011. Secret management: server-side key generation, no material on the surface

- **Status:** Accepted
- **Date:** 2026-09-29
- **Deciders:** NCode.Identity maintainers

## Context

The OpenID management API (`NCode.Identity.OpenId.Management`) exposes CRUD over the secrets owned by an
OpenID **server**, **tenant**, and **client**. These decisions apply to **all** of those secret owners;
server secrets are the first implementation and tenant/client secrets follow the same rules as their
endpoint groups land. Reading secrets was already correct-by-default: `SecretResource` exposes only
metadata (`Use`, `Algorithm`, `CreatedWhen`, `ExpiresWhen`, `SecretType`, `KeySizeBits`) and never the
`EncodedValue`. But there was no write path — a secret collection could only be seeded through
the persistence layer directly (`AddAsync`), not managed at runtime.

Two facts constrain the write path:

- **`PersistedSecret.EncodedValue` is data-protected (encrypted) key material**, base64url-encoded.
  `DefaultSecretSerializer` unprotects it via `IDataProtectorFactory<PersistedSecret>` on the way out;
  anything that writes a secret must protect it symmetrically on the way in.
- **The stores had no per-secret operations** — only whole-collection reads and settings writes.
  Individual add/update/delete did not exist.

Creating a secret therefore raises a security-design fork: does the operator **import** raw private key
material over the wire (BYOK), or does the **server generate** it? Importing private keys means
plaintext key material transits the API and the operator's tooling; generation keeps the private key
inside the server's data-protection boundary from birth.

## Decision

The management API creates secrets — for any owner (server, tenant, client) — by **server-side
generation only** for now. The rules below are written against the server endpoints (the first
implementation); the tenant and client secret endpoints adopt the identical shape and semantics:

- **`POST api/servers/{serverId}/secrets`** takes _metadata_ (`secretType`, `keySizeBits`, optional
  `use`, `algorithm`, `expiresWhen`, optional `secretId`). The server generates fresh key material for
  that `(secretType, keySizeBits)`, data-protects it, base64url-encodes it into `EncodedValue`, and
  persists it. The response is a `SecretResource` (metadata only). Symmetric key material never leaves
  the server; asymmetric public material is not returned yet (a deliberate, additive future option).
- **`PUT api/servers/{serverId}/secrets/{secretId}`** is **metadata-only** — it rotates `expiresWhen`,
  `use`, and `algorithm`. Key material is immutable once generated; rotation is create-new + delete-old.
- **`GET api/servers/{serverId}/secrets/{secretId}`** returns one `SecretResource`;
  **`DELETE`** removes it. Both honor the resource-based authorization and `ETag`/concurrency model the
  other management endpoints use.

Generation lives in a new **`ISecretGenerator`** contract in `NCode.Identity.Secrets.Persistence`
(alongside `DefaultSecretSerializer`, which owns the inverse unprotect path and the same
`IDataProtectorFactory<PersistedSecret>`), so the crypto/protection concern stays in the secrets stack
and every management layer (server, tenant, client) is a thin DI consumer. Per-secret persistence is a
set of new store methods (`GetSecretOrDefaultAsync`, `AddSecretAsync`, `UpdateSecretAsync`,
`RemoveSecretAsync` — on `IServerStore`, `ITenantStore`, and `IClientStore`) that also bump
the owner's `SecretsConcurrencyToken` so the running server/tenant/client secret provider refreshes.
The client store carries no dedicated per-secret read getter; its endpoints resolve a single secret
from the owner-scoped `GetSecretsOrDefaultAsync` (which also supplies the owning `TenantId`).

Key material import (BYOK) is **explicitly deferred**, not rejected — the request/DTO shape leaves room
for an additive `EncodedValue`/PEM import field guarded by a future decision. Code that would host it
carries a `// Future:` note rather than an implementation.

## Options considered

- **Import only (BYOK).** Rejected as the default: it puts plaintext private keys on the wire and in
  operator tooling, widening the attack surface for the common case. Retained as a future additive
  option behind its own review.
- **Both generation and import now.** Rejected for scope and risk: import needs its own validation
  (key format, size, curve, encoding) and threat review; shipping it together would delay the safe,
  common path. Generation ships first; import can be added without breaking the surface.
- **Full-replace `PUT` (metadata + new material).** Rejected: silently replacing key material under a
  stable `secretId` makes rotation ambiguous (old tokens signed by the replaced key look valid by id
  but fail verification). Immutable material + explicit create/delete keeps rotation auditable.
- **Expose generation on `ISecretSerializer`.** Rejected: the serializer's job is encode/decode of
  _existing_ material; generating brand-new keys is a distinct concern that earns its own contract.
- **Do generation inside the Management project.** Rejected: it would pull the data-protection package
  and BCL crypto into the management layer and duplicate the protect/encode logic that already lives
  next to its inverse in the secrets-persistence package.

## Consequences

- The common, safe path (operator asks the server to mint a key) works end-to-end with no private key
  material crossing the API boundary in either direction.
- `SecretResource` stays metadata-only; there is still no public API that returns `EncodedValue`.
- Rotation is create-new + delete-old, which is auditable but requires two calls; a future convenience
  "rotate" endpoint can wrap it.
- BYOK is a known follow-up; the DTO and store are shaped so import is an additive change, not a
  breaking one.
- `ISecretGenerator` is new public surface in `NCode.Identity.Secrets.Persistence(.Abstractions)` and is
  reusable for client/tenant secret management — the tenant and client secret endpoints now consume it
  identically to the server ones.
- Discovering that the secret store initially had no per-secret write path surfaced a latent, provider-independent
  bug: `ConcurrencyTokenSaveChangesInterceptor` only overrode the synchronous `SavingChanges`, so it never fired
  for the `SaveChangesAsync` the stores use. That is resolved on its own terms in
  [ADR-0012](0012-interceptor-managed-concurrency-tokens.md); the secret stores rely on the (now working)
  interceptor for row tokens and eagerly assign the token on insert so `POST` needs no re-read.
- **Individual secret `GET`/`PUT`/`DELETE` are authorized against an owner-scoped wrapper.** A
  `PersistedSecret` loaded by id does not itself carry its owner, so authorizing the bare entity would leave
  `TenantAdminHandler` (which only evaluates `ISupportTenantId` resources) unable to scope it — a scoped admin
  would fall through to the `GlobalAdmin`-only path. The tenant individual-secret operations therefore wrap the
  loaded secret in `TenantOwnedResource<PersistedSecret>`, carrying the route `tenantId` (trustworthy because
  `GetSecretOrDefaultAsync(tenantId, secretId)` only returns a secret owned by that tenant), so the handler
  scopes them exactly like the owner/collection operations. The wrapper delegates `ConcurrencyToken` to the
  wrapped secret, leaving the `GET` ETag / `If-None-Match` flow unchanged. Server secret operations stay
  unwrapped and remain `GlobalAdmin`-only by design (servers are not tenant-scoped). The client secret
  endpoints follow the same wrapper pattern: because a client is addressed by a flat `clientId` route with
  no tenant segment, the handler resolves the owning `TenantId` from `GetSecretsOrDefaultAsync` and wraps
  the loaded secret in `TenantOwnedResource<PersistedSecret>` before authorizing.

## Owner lifecycle (server / tenant / client entities)

Managing an owner's secrets presumes the owner exists, so the same management layer also exposes CRUD over
the **owner entities themselves** — the server, tenant, and client rows that own the secret collections.
These endpoints share the secret endpoints' authorization, concurrency, and (from
[ADR-0015](0015-management-core-validation-via-validators.md)) core-validation model:

- **Create** (`POST api/servers`, `POST api/tenants`, `POST api/clients`) persists the owner row plus an
  **empty** settings and secrets collection. It never accepts key material — a freshly created owner starts
  with zero secrets and is populated through the secret endpoints above. Uniqueness (and, for a client, the
  existence of its parent tenant) is asserted **before** the write by the entity validator rather than
  inferred from a caught store exception, so a duplicate is a `409 Conflict` and a missing parent tenant is a
  `400 Bad Request` with a deterministic body. The store eager-assigns the row `ConcurrencyToken` on insert
  (ADR-0012), so the `201 Created` carries a correct `ETag` with no re-read.
- **Update** (`PATCH api/tenants/{tenantId}`, `PATCH api/clients/{clientId}`) is a JSON Patch over the owner's
  own mutable metadata (e.g. `isDisabled`, tenant `domainName`/`displayName`), guarded by an optional
  `If-Match`. There is **no server update endpoint**: `PersistedServer` has no mutable top-level fields — its
  mutable state lives entirely in its settings and secrets sub-resources, which have their own endpoints.
- **Delete** (`DELETE api/servers/{serverId}`, `DELETE api/tenants/{tenantId}`, `DELETE api/clients/{clientId}`)
  is **dependency-guarded**. The entity validator rejects the delete with `409 Conflict` while the owner
  still has dependents (a tenant with clients or tenant-secrets, a client with client-secrets, a server with
  server-secrets), so removal is only ever a leaf operation. The store's `RemoveAsync` re-checks the same
  invariant, so the guard holds even if a caller bypasses the endpoint.

All owner operations authorize the loaded/constructed entity exactly like the secret operations: tenant and
client entities are tenant-scoped (`ISupportTenantId` / `TenantOwnedResource`), servers stay `GlobalAdmin`-only.

## References

- [ADR-0002](0002-ephemeral-development-keys.md) — development key generation and the data-protection boundary.
- [ADR-0010](0010-supported-settings-unset-means-unrestricted.md) — the settings cascade the sibling
  settings endpoints read.
- [ADR-0012](0012-interceptor-managed-concurrency-tokens.md) — how the secret (and every) store's concurrency
  token is generated.
- [ADR-0015](0015-management-core-validation-via-validators.md) — the entity validators that
  authorize and validate every management create/update/delete (including the owner lifecycle above).
- `NCode.Identity.Secrets.Persistence.Logic.DefaultSecretSerializer` — the inverse unprotect path.
- `NCode.Identity.OpenId.Persistence.Stores.IServerStore` — the extended store contract.
