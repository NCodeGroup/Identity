# 104. Import of caller-supplied public asymmetric secret material (BYOK)

- **Status:** Accepted (client keys amended by
  [ADR-0161](0161-a-clients-public-keys-have-one-source-and-are-imported-under-one-policy.md): a client's key is
  imported under one client-key policy, keeps its own `kid`, and registration no longer stores the `jwks` document;
  import formats amended by [ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md): a format is an open name
  each key type declares, see decision 6)
- **Date:** 2026-10-15
- **Deciders:** NCode Group

## Context

Secrets were **server-generation-only**: `ISecretGenerator` mints fresh key material server-side, data-protects the
private key, and never accepts caller-supplied material ([ADR-0011](0011-secret-management-api.md)). That fits the
tenant's own signing keys, but it cannot onboard a **relying party's public key** — the shape `private_key_jwt`
(OpenID Connect Core §9), JWT-Secured Authorization Requests (JAR), and `self_signed_tls_client_auth`
([RFC 8705](https://www.rfc-editor.org/rfc/rfc8705) §2.2) all need: the client generates its own key pair and
registers only the **public** half.

[ADR-0103](0103-public-only-asymmetric-secret-keys.md) added the in-memory primitive — verify-only
`PublicRsaSecretKey` / `PublicEccSecretKey` and `ISecretKeyFactory.CreateRsaPublicKey` / `CreateEccPublicKey`, and a
relaxed `Create(certificate)` that yields a verify-only key for a certificate without a private half — but stopped
there. The surrounding layers have no complementary support:

- **Persistence cannot round-trip a bare public key.** `DefaultSecretSerializer` dispatches on `SecretType` and
  decodes `rsa` / `ecc` as **PKCS#8 private** keys (`CreateRsaPkcs8` / `CreateEccPkcs8`). Only the `x509` branch
  round-trips public material, because `Create(certificate)` auto-detects a public-only certificate. A **bare public
  key** — a JWK `n`/`e` or `crv`/`x`/`y` with no certificate, the native JWKS shape — cannot be persisted at all.
- **No producer** turns caller-supplied material into a `PersistedSecret`: `ISecretGenerator` generates only, and
  refuses `Certificate`.
- **No JWK → key parser** exists (only the key → JWK export path, `IJsonWebKeyConverter`).
- The **management API** and **Dynamic Client Registration** can therefore neither import a public certificate nor a
  JWK; DCR stores `jwks` / `jwks_uri` as inert settings metadata that nothing consumes.

The persistence model also **conflates two orthogonal axes** in the single `SecretType` discriminator: the _crypto
family_ (`symmetric` / `rsa` / `ecc`) and the _stored container_ (`x509` is a container, not a family). Representing a
public bare key needs the serializer to know whether the stored asymmetric bytes are a **private PKCS#8** or a **public
SubjectPublicKeyInfo (SPKI)**, which that single axis cannot express.

## Decision

**Add a first-class import path for public asymmetric material, and make the persistence form an explicit, orthogonal
axis so public keys round-trip cohesively.**

1. **New persisted discriminator `SecretEncoding`** on `PersistedSecret` (and the `SecretEntity` column + both
   mappings), describing exactly how the stored bytes decode — independent of the `SecretType` crypto family:
    - `symmetric_raw` — raw symmetric key bytes.
    - `pkcs8` — PKCS#8 DER **private** key (`rsa` / `ecc`).
    - `spki` — SubjectPublicKeyInfo DER **public** key (`rsa` / `ecc`).
    - `x509` — X.509 certificate DER (public; a relying party's registered certificate).

    `DefaultSecretSerializer` dispatches on `(SecretType, SecretEncoding)`: `spki` + `rsa` → `CreateRsaPublicKey`,
    `spki` + `ecc` → `CreateEccPublicKey`, making the ADR-0103 verify-only factories reachable from the store. The
    stored bytes remain **data-protected uniformly** (the protector is an at-rest concern, applied to all material
    regardless of confidentiality; public material is protected only incidentally, not because it is secret).

2. **New importer producer `ISecretImporter`** (in `NCode.Identity.Secrets.Persistence.Abstractions`, beside
   `ISecretGenerator`) taking an `ImportSecretRequest` that carries the material in one of three caller formats —
   **JWK** (JSON), **PEM**, or **X.509 certificate** (base64 DER) — plus the same metadata as generation
   (`SecretId` / `Use` / `Algorithm` / `CreatedWhen` / `ExpiresWhen`). It parses and validates the material, derives
   the crypto family and `KeySizeBits`, selects the `SecretEncoding`, data-protects the canonical DER, and returns a
   ready-to-persist `PersistedSecret`.

3. **Import accepts public material only.** A JWK carrying a private component (`d`), a PEM private key, or a
   certificate with a private key is **rejected**. Importing private key material is a distinct threat model
   (reversible storage, escrow) and remains out of scope. Symmetric import is likewise out of scope — symmetric client
   secrets are server-generated and revealed once ([ADR-0050](0050-secret-material-revealed-once-on-create.md)).

4. **JWK → key parsing lives in the Secrets slice** (no dependency on the OpenID JWK result types, which sit in a
   higher layer): a small reader turns the JWK `kty` / `n` / `e` / `crv` / `x` / `y` members into `RSAParameters` /
   `ECParameters` and through `ISecretKeyFactory.CreateRsaPublicKey` / `CreateEccPublicKey`. Both the management API and
   DCR reuse it through `ISecretImporter`.

5. **Surfacing:**
    - **Management API** gains `POST .../secrets/import` on every secret owner (server, tenant, client, resource
      server), mirroring the generate endpoint but returning the material-free `SecretResource` (there is no one-time
      material to reveal for a public import) and recording the same `…SecretChanged` audit event.
    - **DCR** consumes an **inline `jwks`** JWK Set at registration, importing each public key as a persisted `spki` /
      `x509` secret so the registered client's `SecretKeys` actually carry its verification keys. **Runtime `jwks_uri`
      fetch + cache + SSRF guard remains deferred** to `F16` Phase 0 — this ADR covers static import only.
    - **Capabilities** (`GET /api/capabilities`, [ADR-0067](0067-secret-type-capability-catalog-and-capabilities-discovery.md))
      advertises the supported import formats per secret type.

6. **An import format is an open name, and each key type declares the formats it parses.** `jwk`, `pem`, and
   `certificate` are the built-in `SecretKeyImportFormats` names, not the members of a closed enum: a key type a
   package registers may parse PKCS #12, an OpenSSH public key, or a key-vault reference, so the format set is open
   ([ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md)). `ISecretKeyTypeDescriptor.PublicKeyImportFormats`
   lists the names a type's `ImportPublicKeyMaterial` accepts and is empty for a type that imports nothing, such as
   `symmetric`. Every entry point checks the requested name against that list — the management validator, the
   importer, and the client-key importer — and rejects an unlisted name with a message that lists the type's formats.
   Names compare ordinally, so `JWK` is not `jwk`. RSA and ECC accept all three built-in names; OKP and AKP accept
   `jwk` and `pem`, since they have no certificate form here. The capabilities flag `supportsPublicKeyImport` is true
   when the list is not empty.

## Options considered

- **Reuse the `x509` type for all public keys (cert-only import).** Rejected. A bare JWK (`n`/`e`) is not a
  certificate and wrapping it in a synthesized self-signed certificate fabricates metadata (subject, validity) the
  party never supplied. JWK Sets — the dominant publication format and the content of a `jwks_uri` — carry bare JWKs,
  so cert-only import cannot represent the primary onboarding shape. The orthogonal `SecretEncoding` axis supports both
  bare keys and certificates with one model.

- **Encode public-ness in new `SecretType` values (`rsa-public` / `ecc-public`).** Rejected. It fragments the type
  taxonomy and forks the runtime `SecretKeyTypes` mapping (which keys a `SecretType` to a CLR key class, not to a
  public/private distinction). Public vs. private is genuinely orthogonal to the crypto family — a separate axis models
  it without multiplying type constants.

- **Detect SPKI vs. PKCS#8 implicitly in the serializer (try-import, fall back).** Rejected. It relies on exception
  control flow over cryptographic imports and leaves the stored form ambiguous; the codebase favors explicit
  discriminators (`SecretType` is explicit, and `x509` is an explicit container marker).

- **Accept private-key import now.** Rejected (scope). A reversible-at-rest private import is a separate threat model
  (`S5` storage fork, escrow, key-ceremony) and is not needed for the public-key onboarding that `private_key_jwt` /
  mTLS / JAR require.

## Consequences

- A relying party's public key — as a **JWK**, **PEM**, or **certificate** — becomes a first-class persisted
  `SecretKey` that round-trips through the store and is consumed by client authentication and JAR verification,
  unblocking `private_key_jwt` / `self_signed_tls_client_auth` end-to-end onboarding (`F16` Phase 1).
- **Breaking persistence change:** `PersistedSecret` / `SecretEntity` gain a required `SecretEncoding` column (an EF
  migration backfills existing rows: `symmetric` → `symmetric_raw`, `x509` → `x509`, `rsa` / `ecc` → `pkcs8`). Accepted
  under the greenfield, pre-release posture ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)).
- New public surface: `ISecretImporter` + `ImportSecretRequest` (persistence); `SecretEncoding` on `PersistedSecret`;
  `ImportSecretRequest` + `POST .../secrets/import` management contracts; a JWK reader in the Secrets slice.
- The importer is **public-only by construction**, so no private or symmetric material is ever accepted on an import
  path — the reveal-once model ([ADR-0050](0050-secret-material-revealed-once-on-create.md)) remains the sole way a
  symmetric secret value leaves the server, and private asymmetric keys are still never caller-supplied.
- Runtime `jwks_uri` resolution stays out of scope (deferred to `F16` Phase 0), so import is a deterministic,
  SSRF-free operation over caller-supplied bytes.
  </content>
  </invoke>
