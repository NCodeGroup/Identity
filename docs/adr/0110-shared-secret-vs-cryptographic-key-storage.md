# 110. A shared secret is stored as the UTF-8 bytes of its opaque string, distinct from a cryptographic key

- **Status:** Accepted
- **Date:** 2026-10-15
- **Deciders:** NCode Group

## Context

A symmetric `PersistedSecret` serves two fundamentally different purposes that had been conflated behind a single
generation path (`ISecretGenerator.GenerateSecret`):

1. **A cryptographic key** the server itself uses — an `HS256` signing key, or a symmetric JWE key for
   audience-specific token encryption ([ADR-0075](0075-audience-specific-secret-encryption.md)). Here the stored key
   material **is** the raw CSPRNG bytes, and the one-time reveal ([ADR-0050](0050-reveal-once-secret-material.md)) is
   their `base64url` form (the JWK `k`). An external party that needs the key imports that `base64url` value.

2. **A shared secret** an external party presents verbatim — an OAuth **`client_secret`**. It is consumed in two ways,
   and **both** treat the secret as the UTF-8 bytes of an opaque string:
    - `client_secret_basic` / `client_secret_post` compare the presented secret to the stored material. Client
      authentication does this by taking `UTF8(presented client_secret)` and byte-comparing it to the stored symmetric
      key material (`CommonClientAuthenticationHandler.IsSecretEqual`).
    - `client_secret_jwt` (OpenID Connect Core §9) derives the `HS256` MAC key from the **UTF-8 octets of the
      `client_secret`**; the handler verifies with the stored symmetric key material as that MAC key.

    So a `client_secret` only works if its stored key material equals `UTF8(client_secret_string)`.

`GenerateSecret` for a symmetric type stores **raw** random bytes and reveals `base64url(raw)`. That is correct for
case 1 but **wrong for case 2**: `UTF8(base64url(raw)) ≠ raw`, so a generated `client_secret` could never authenticate.
The bug was latent until **Dynamic Client Registration** ([ADR-0063](0063-dynamic-client-registration.md)) issued
client secrets server-side and the external OpenID conformance suite drove `client_secret_basic` against them — every
token-endpoint authentication failed with `invalid_client`. The same latent defect existed on the management
"create client secret" endpoint whenever an administrator generated a symmetric secret for a client. The existing
config-seeded clients were unaffected because they persist the UTF-8 bytes of a chosen string directly (the seed
`base64url`-decodes a transport encoding whose bytes already **are** `UTF8(secret)`), which is the correct shape.

## Decision

**Model "shared secret" and "cryptographic key" as two distinct generation operations, and store a shared secret as
the UTF-8 bytes of its opaque string.**

1. **`ISecretGenerator.GenerateSharedSecret(GenerateSecretRequest)`** is the single correct-by-default source for
   issuing a presented secret. It generates a URL-safe random string (the one-time `SecretMaterial` reveal) and
   persists a `symmetric_raw` secret whose stored key material is `UTF8(string)`. `GenerateSecret` keeps its
   raw-bytes / `base64url`-reveal semantics for cryptographic keys. The request's `SecretType` must be symmetric.

2. **Every site that issues a `client_secret` routes through `GenerateSharedSecret`:** the DCR endpoint
   (`DefaultClientRegistrationEndpointHandler`) and the management client-secret endpoint
   (`ClientApiEndpointHandler.CreateSecretAsync`, which dispatches symmetric → `GenerateSharedSecret`, asymmetric →
   `GenerateSecret`). A **client's** symmetric secret is always its `client_secret`; a **server / tenant /
   resource-server** symmetric secret is a server-side cryptographic key and stays on `GenerateSecret`.

## Consequences

- A generated `client_secret` authenticates round-trip across `client_secret_basic`, `client_secret_post`, and
  `client_secret_jwt`, because the stored material is exactly what each path compares against or keys from.
- The crypto-key-vs-shared-secret distinction is explicit at the call site and documented here, so a future secret
  issuer picks the right method rather than silently re-introducing the raw-bytes defect. Persisting a **given**
  shared-secret string (the seed handlers / integration-test factory do this inline today) is a candidate for the same
  consolidation — tracked in [`BACKLOG.md`](../../BACKLOG.md).
- Public API grows by one method on `ISecretGenerator`; no storage-shape or migration change (both kinds remain
  `symmetric_raw` `PersistedSecret`s).
