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
`RemoveSecretAsync` — on `IServerStore` today, on the tenant/client stores as those land) that also bump
the owner's `SecretsConcurrencyToken` so the running server/tenant/client secret provider refreshes.

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
  reusable for client/tenant secret management when those endpoint groups land.
- The store methods **assign concurrency tokens manually** (they do not rely on
  `ConcurrencyTokenSaveChangesInterceptor`). That interceptor only overrides the synchronous
  `SavingChanges`, so it never fires for the `SaveChangesAsync` the stores use — a latent, provider-independent
  gap that every store already works around by setting its own tokens. Fixing the interceptor to also handle
  the async path is a separate, cross-cutting decision (it would double-assign the manually managed tokens).

## References

- [ADR-0002](0002-ephemeral-development-keys.md) — development key generation and the data-protection boundary.
- [ADR-0010](0010-supported-settings-unset-means-unrestricted.md) — the settings cascade the sibling
  settings endpoints read.
- `NCode.Identity.Secrets.Persistence.Logic.DefaultSecretSerializer` — the inverse unprotect path.
- `NCode.Identity.OpenId.Persistence.Stores.IServerStore` — the extended store contract.
