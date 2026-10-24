# 105. The secret-key-type descriptor is the first-class seam for every key operation

- **Status:** Accepted
- **Date:** 2026-10-15
- **Deciders:** NCode Group

## Context

The secret-key-type descriptor ([ADR-0067](0067-secret-type-capability-catalog-and-capabilities-discovery.md)) began
life as a **taxonomy + capability catalog** — the single source of truth for _which_ key types exist and _what_ they
can do (`KeyType`, `IsAsymmetric`, `SupportsPublicKeyExport`, plus `ValidateGeneration` / `GenerateKeyMaterial`). As
public-key import ([ADR-0104](0104-import-public-asymmetric-secret-material.md)) landed, more per-key-type behavior was
needed, and the codebase had accreted **three parallel places** where a key type's behavior lived:

- The **descriptor** owned generate + validate (and the import logic was being added here).
- `DefaultSecretSerializer` owned **deserialize** as a hardcoded `switch` on `SecretType`.
- A separate `IJsonWebKeyConverter` strategy registry (RSA/ECC converters in the OpenID layer) owned **JWK export** —
  a second per-key-type strategy collection duplicating the descriptor's role.

Adding a new key type therefore meant touching several unrelated seams, and importing a type the deserializer did not
know about would persist but fail to round-trip. The descriptor was a first-class citizen for _some_ operations and a
bystander for others.

Separately, the runtime `SecretKey` implementations duplicated state: `DefaultRsaSecretKey` / `PublicRsaSecretKey`
(and the ECC pair) each re-declared identical `Metadata` / `KeySizeBits` / certificate handling, differing only in how
they export their key material.

## Decision

**Make `ISecretKeyTypeDescriptor` the single first-class seam for the entire lifecycle of a secret key type**, and
fold the parallel registries and duplicated state into it.

1. **The descriptor owns every per-key-type operation:** `ValidateGeneration`, `GenerateKeyMaterial`, **import**
   (`ImportPublicKeyMaterial`, [ADR-0104](0104-import-public-asymmetric-secret-material.md)), **deserialize**
   (`DeserializeKeyMaterial`), and **export** (`ExportPublicKey` → a `JSON Web Key` object + a `SubjectPublicKeyInfo`
   PEM), plus the capabilities (`SupportsPublicKeyExport` and the `PublicKeyImportFormats` names the type imports).
   Registering one descriptor adds a key type **end-to-end**.

2. **The persistence services become thin wrappers over the catalog.** `DefaultSecretGenerator`,
   `DefaultSecretImporter`, and `DefaultSecretSerializer` now resolve the descriptor by `SecretType` and delegate; they
   retain only the persistence concerns (data-protection and `PersistedSecret` assembly). The serializer's hardcoded
   `SecretType` switch is gone — the exception-driven "try one algorithm then another" import guessing it would have
   required never existed.

3. **The `IJsonWebKeyConverter` registry is collapsed into the descriptor.** `descriptor.ExportPublicKey` produces the
   key-type-specific JWK members (as a `System.Text.Json.Nodes.JsonObject`); the JWKS endpoint keeps only the _document_
   assembly, and the management public-key read path consumes the same seam. The `IJsonWebKeyConverter`,
   `JsonWebKeyConverter<T>`, and the typed `JsonWebKey` / `RsaJsonWebKey` / `EccJsonWebKey` result classes are removed.

4. **The extension bases are public, and only the asymmetric base depends on the factory.** `SecretKeyTypeDescriptor`
   and `AsymmetricSecretKeyTypeDescriptor` become public `abstract` classes so an application can derive from them to
   add a custom key type, reusing the shared import / deserialize / export dispatch and supplying only the
   family-specific hooks. The dependency-free `SecretKeyTypeDescriptor` base takes **no** constructor dependencies, so a
   custom type that constructs its own `SecretKey` derives from it directly; `ISecretKeyFactory` is injected one level
   down, on `AsymmetricSecretKeyTypeDescriptor` (where the built-in RSA/ECC/certificate construction primitives are
   actually used), instead of being forced onto every descriptor. `ISecretKeyFactory` is thereby a **closed, low-level
   convenience primitive for the built-in families — not the extensibility seam**: the descriptor is.

5. **Shared `SecretKey` state is lifted to the base.** `AsymmetricSecretKey` now holds `Metadata`, `KeySizeBits`, and
   the optional certificate (with `HasCertificate` / `ExportCertificate`); `RsaSecretKey` / `EccSecretKey` own the
   uniform `ExportAsymmetricAlgorithm` and (for ECC) `GetECCurve`. The `Default*` / `Public*` concretes now hold only
   their key material and the one `Export*` method that differs, eliminating the duplication across the four classes.

6. **A certificate is not a distinct secret type.** With the stored-form axis (`SecretEncoding`,
   [ADR-0104](0104-import-public-asymmetric-secret-material.md)), a certificate is simply a crypto **family**
   (`rsa` / `ecc`) stored with the `x509` encoding. The former persistence-only `SecretTypes.Certificate` ("x509")
   discriminator is **removed**: `SecretType` is now exactly the descriptor's key-type (the catalog key), so every
   persisted secret has a descriptor that can round-trip it.

## Options considered

- **Keep the three parallel seams and just add import to the descriptor.** Rejected. It leaves `IJsonWebKeyConverter`
  duplicating the descriptor's job and the serializer's hardcoded switch unable to round-trip a custom imported type —
  the extension point would be a half-measure.

- **Detect the import key type by trying each algorithm (RSA, then ECC).** Rejected in favor of a required `SecretType`
  discriminator that selects the descriptor deterministically — no exception-driven control flow, and the catalog is
  the extension point.

- **Keep `SecretTypes.Certificate` for back-compat.** Rejected. A public constant whose values cannot be deserialized
  (no catalog descriptor is keyed to `x509`) is a latent trap; the greenfield posture
  ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)) favors removing it over preserving a broken shape.

- **A separate `PublicKeySecretKey` hierarchy / per-family duplication of certificate state.** Rejected. Lifting the
  shared state onto `AsymmetricSecretKey` keeps a single source of truth without forking key resolution.

## Consequences

- One registration — an `ISecretKeyTypeDescriptor` — teaches the whole system (generate, import, export, deserialize,
  JWKS publication, management capabilities) about a new key type. The descriptor is the documented, public extension
  seam; `ISecretKeyFactory` remains the lower-level primitive the descriptors build keys with.
- **Breaking public-surface churn** (expected pre-release, [ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)):
  `IJsonWebKeyConverter` and the typed `JsonWebKey` classes are removed (the JWKS `keys` array is now JSON objects);
  `SecretTypes.Certificate` is removed; `SecretKeyTypeDescriptor` / `AsymmetricSecretKeyTypeDescriptor` are now public;
  the persistence generator/importer/serializer and the descriptor interface gain/lose members.
- The duplicate per-key-type JWK strategy registry and the duplicated `SecretKey` state are gone, so the two classes of
  drift they invited (a key type exported but not deserialized, or metadata handled differently per concrete) cannot
  recur.
