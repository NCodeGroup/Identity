# 103. Public-only (verify-only) asymmetric secret keys

- **Status:** Accepted
- **Date:** 2026-10-14
- **Deciders:** NCode Group

## Context

The `SecretKey` model was **private-key-only**. `DefaultRsaSecretKey` / `DefaultEccSecretKey` store a data-protected
PKCS#8 **private** key, `ISecretKeyFactory.CreateRsa` / `CreateEcc` export that private key before wrapping it, and
`ISecretKeyFactory.Create(X509Certificate2)` **threw** when the certificate carried no private key. This fit the
original use — the tenant's own signing keys, which the OP both signs and verifies with.

JWT-bearer client authentication ([ADR-0102](0102-jwt-bearer-client-authentication.md)), JWT-Secured Authorization
Requests (JAR), and client `jwks_uri` / BYOK onboarding all need the **opposite**: the OP holds a relying party's
**public** key and only ever **verifies** with it. A public key is not secret and the party never shares its private
half, so there was no way to represent such a key — a client's registered public certificate or JWKS entry could not
become a usable `SecretKey`, which blocked `private_key_jwt` end-to-end and all client public-key onboarding.

## Decision

**Add verify-only asymmetric secret keys that hold only the public half, and let `Create(certificate)` produce one for a
certificate without a private key.**

- **`PublicRsaSecretKey` and `PublicEccSecretKey`** subclass the existing `RsaSecretKey` / `EccSecretKey` abstractions
  and store a `SubjectPublicKeyInfo` (SPKI) rather than a protected PKCS#8 private key. `ExportRSA()` / `ExportECDsa()`
  import the SPKI, so **verification flows are unchanged** — the signature algorithms call the same `ExportRSA()` /
  `ExportECDsa()` and `VerifyData`. Signing is unavailable (the exported key has no private half and throws), which is
  correct for a key only ever used to verify. The public key is **not secret, so it is stored unprotected** (no data
  protector), unlike the private-key variants.

- **New factory methods `CreateRsaPublicKey` / `CreateEccPublicKey`** build these keys from an `RSA` / `ECAlgorithm`
  carrying only public parameters (the shape JWK `n`/`e` and `crv`/`x`/`y` import into), optionally attaching the
  originating certificate.

- **`ISecretKeyFactory.Create(KeyMetadata, X509Certificate2)` now accepts a public-only certificate.** When the
  certificate has no private key it yields a verify-only key built from the certificate's public key, instead of
  throwing `ArgumentException`. This makes a relying party's registered **public certificate** (persisted as an `x509`
  secret) a usable verification key with no new secret type.

## Options considered

- **Keep rejecting public-only material; require callers to hold private keys.** Rejected. It is impossible for the
  genuine use case — a relying party registers only its public key — and conflicts with the greenfield posture of
  modeling the correct shape ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)).

- **A separate `PublicKeySecretKey` type outside the `RsaSecretKey` / `EccSecretKey` hierarchy.** Rejected. Subclassing
  the existing abstractions means every consumer that resolves keys by `RsaSecretKey` / `EccSecretKey` and the signature
  algorithms (`SecretKey.Validate<RsaSecretKey>`) work unchanged; a parallel hierarchy would fork key resolution.

- **Store the public key data-protected like the private variants.** Rejected. A public key is not confidential;
  protecting it adds cost and key-management coupling with no security benefit, and would obscure that the material is
  public.

## Consequences

- The OP can hold a relying party's public key as a first-class `SecretKey`, unblocking `private_key_jwt`
  ([ADR-0102](0102-jwt-bearer-client-authentication.md)) and JAR verification against a client's registered keys, and
  providing the primitive the client `jwks_uri` / BYOK onboarding (`F16` Phase 0, `S3`) builds on.
- A client registered with a **public certificate** (`x509` secret) now authenticates via `private_key_jwt` with no new
  secret type: `DefaultSecretSerializer` deserializes the certificate through the relaxed `Create`, producing a
  verify-only key. (When persisting such a secret, `KeySizeBits` must be the public key's size — e.g. 2048 for RSA —
  because `DefaultSecretSerializer` asserts `SecretKey.KeySizeBits` equals the persisted value.)
- New public surface: `ISecretKeyFactory.CreateRsaPublicKey` / `CreateEccPublicKey`; `Create(certificate)` no longer
  throws for a public-only certificate (a behavior change, acceptable pre-GA).
- A verify-only key cannot sign; using one where signing is expected fails fast with a cryptographic error, which is the
  intended contract.
