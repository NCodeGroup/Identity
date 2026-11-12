# 1104. A key type is one registered descriptor, and public keys are imported and stored by encoding

- **Status:** Accepted
- **Date:** 2026-10-16
- **Deciders:** NCode Group

## Context

A secret key type has several operations: it is generated, imported, deserialized from storage, and exported as a JSON
Web Key and a SubjectPublicKeyInfo (SPKI) PEM. When each operation lives in its own place, a new key type touches
several unrelated seams, and a type the deserializer does not know persists but does not round-trip. One strategy
registry per operation duplicates the role of the catalog that already says which key types exist and what they can do.

A relying party's key cannot be onboarded if the server only generates keys. `private_key_jwt` (OpenID Connect Core
section 9), JWT-Secured Authorization Requests (JAR) and `self_signed_tls_client_auth`
([RFC 8705](https://www.rfc-editor.org/rfc/rfc8705) section 2.2) need the client to generate its own key pair and
register only the public half, as a JWK, a PEM or a certificate. A server-generated secret is revealed once and never
cached ([ADR-1050](1050-a-secret-is-generated-by-the-server-revealed-once-and-never-cached.md)), so caller-supplied
material needs its own path.

The persisted form has two independent facts. The crypto family (`symmetric`, `rsa`, `ecc`, `okp`, `akp`) says what the
key is. The stored container says how the bytes decode: a PKCS#8 private key, an SPKI public key or an X.509
certificate. One discriminator cannot express both, and a certificate is a container of an `rsa` or `ecc` key, not a
key type of its own.

The runtime key classes need a verify-only form that holds only the public half. Exporting a public key through
`AsymmetricAlgorithm` ties the contract to the RSA and ECDSA families, which leaves no room for Edwards-curve and
post-quantum keys.

## Decision

**A key type is one registered `ISecretKeyTypeDescriptor`. Caller-supplied public keys are imported through it and
stored by an encoding that is independent of the key type.**

1. **The descriptor is the one extension seam for a key type.** `ISecretKeyTypeDescriptor` declares `KeyType`,
   `IsAsymmetric`, `ValidateGeneration`, `GenerateKeyMaterial`, `ImportPublicKeyMaterial`, `DeserializeKeyMaterial`,
   `ExportPublicKey` (the JWK members as a `JsonObject` and the SPKI PEM), `SupportsPublicKeyExport` and
   `PublicKeyImportFormats`. `ISecretKeyTypeCatalog` is the one place a key type is described. Registering one
   descriptor adds a type end to end: generate, import, deserialize, JWKS and capabilities
   ([ADR-1067](1067-secret-key-types-form-one-catalog-and-authenticated-management-callers-discover-the-deployments-capabilities.md)).
   No generator, validator, serializer or converter switches on a key type.

2. **The persistence services only persist.** `DefaultSecretGenerator`, `DefaultSecretImporter` and
   `DefaultSecretSerializer` resolve the descriptor by `SecretType` and delegate to it. They keep the persistence
   concerns: data protection and the assembly of the `PersistedSecret`. `SecretType` is exactly the descriptor's key
   type. A certificate is an `rsa` or `ecc` key stored with the `x509` encoding.

3. **The extension bases are public.** `SecretKeyTypeDescriptor` is a public abstract base with no constructor
   dependencies, so a custom type that builds its own `SecretKey` derives from it. `AsymmetricSecretKeyTypeDescriptor`
   is a public abstract base that takes `ISecretKeyFactory`, where the built-in RSA, ECC and certificate construction
   is used. `ISecretKeyFactory` is a low-level primitive for the built-in families, not the extension seam.

4. **`SecretEncoding` says how stored bytes decode.** `PersistedSecret.SecretEncoding` is independent of the crypto
   family. `SecretEncodings` names `symmetric_raw`, `pkcs8`, `spki`, `x509`, `verifier` and `remote`. `IsPublic`,
   `IsRemote`, `IsUnprotected` and `IsVerifier` classify an encoding. The serializer dispatches on
   `(SecretType, SecretEncoding)`.

5. **Verify-only keys hold only the SPKI.** `PublicRsaSecretKey` and `PublicEccSecretKey` subclass `RsaSecretKey` and
   `EccSecretKey`, verify unchanged and cannot sign. `ISecretKeyFactory.CreateRsaPublicKey` and `CreateEccPublicKey`
   build them, and `Create(KeyMetadata, X509Certificate2)` yields one for a certificate without a private key.

6. **The base key state is shared, and the public primitive is a buffer writer.** `AsymmetricSecretKey` holds
   `Metadata`, `KeySizeBits` and the optional certificate (`HasCertificate`, `ExportCertificate`). Its universal public
   primitive is `ExportSubjectPublicKeyInfo<TWriter>(ref TWriter) where TWriter : IBufferWriter<byte>, allows ref
   struct`, not `AsymmetricAlgorithm`, so Edwards-curve and post-quantum keys fit. `ExportSubjectPublicKeyInfoPem()`
   writes through a non-sensitive pooled writer
   ([ADR-1266](1266-binary-data-uses-bufferfactory-rentals-with-explicit-sensitivity-and-text-only-strict-encodings.md)).
   Operations stay on algorithm seams, and `ExportRSA()` and `ExportECDsa()` remain family accessors.

7. **`ISecretImporter` turns supplied public material into a `PersistedSecret`.** `ImportSecret(SecretKeyImportRequest)`
   sits beside `ISecretGenerator` and takes public material in a named format. It derives the family and `KeySizeBits`
   and selects the `spki` or `x509` encoding. The descriptor parses the JWK members itself, so the Secrets slice has no
   dependency on the OpenID JWK types.

8. **Import accepts public material only.** A JWK with `d`, a private PEM, a certificate with a private key and
   symmetric material are refused. Private import is another threat model, and a symmetric secret leaves the server
   only by its one-time reveal.

9. **Import formats are an open vocabulary.** `SecretKeyImportFormats` names `jwk`, `pem` and `certificate`, compared
   ordinally ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)). Each descriptor declares its own
   `PublicKeyImportFormats`: RSA and ECC accept all three, OKP and AKP accept `jwk` and `pem`, and symmetric accepts
   none. The management validator, the importer and `IClientKeyImporter` refuse an unlisted name and list the type's
   formats in the message. Capabilities report `supportsPublicKeyImport`.

10. **Every secret owner can import.** `POST .../secrets/import` takes `ImportSecretRequest { SecretType, Format,
    KeyMaterial, ... }` on every owner, answers the material-free `SecretResource` and records the owner's
    secret-changed audit event. A client's and a dynamically registered client's keys are imported under the client-key
    policy ([ADR-1161](1161-every-owners-public-keys-have-one-source-and-are-imported-under-one-key-policy.md)).

## Options considered

- **Certificate-only import.** Rejected. A bare JWK is not a certificate, and wrapping it in a synthesized certificate
  fabricates metadata the party never supplied. JWK Sets carry bare JWKs, so the primary onboarding shape needs the
  encoding axis.
- **Public-ness in `SecretType`** (`rsa-public`, `ecc-public`). Rejected. It fragments the type taxonomy and forks the
  mapping from a type to a key class. Public versus private is orthogonal to the crypto family.
- **Trial import to detect SPKI or PKCS#8.** Rejected. It relies on exception control flow and leaves the stored form
  ambiguous. An explicit encoding is unambiguous.
- **Private-key import.** Rejected. Reversible storage of a private key is a separate threat model, and public
  onboarding does not need it.
- **A parallel JWK-converter registry or a serializer switch.** Rejected. Each duplicates the descriptor's role and
  adds a seam to touch for every key type.
- **Trying RSA, then ECC.** Rejected. The format and key type are named, so guessing adds ambiguity and exception
  control flow.
- **A separate public-key class hierarchy.** Rejected. Verify-only subclasses of the private-capable classes share the
  verification code and the metadata.
- **`AsymmetricAlgorithm` as the base primitive.** Rejected. It cannot hold Edwards-curve or post-quantum keys.
- **Chosen.** One descriptor per key type, an explicit `SecretEncoding`, public-only import, and a buffer-writer SPKI
  primitive.

## Consequences

- A relying party's JWK, PEM or certificate is a persisted `SecretKey` that round-trips through the store and that
  client authentication and JAR verification consume.
- Import is a deterministic operation over supplied bytes. It makes no outbound request, so it has no SSRF surface.
- A consumer adds a key type by registering one descriptor, and nothing else changes. The base classes supply the
  shared import, deserialize and export dispatch.
- The importer accepts public material only, so no private or symmetric material enters through an import path.
- Tests cover the descriptors, the serializer's dispatch on `(SecretType, SecretEncoding)`, the verify-only keys, the
  refusal of private and symmetric material, the per-type format lists and the import endpoint on each owner.

## References

- `src/NCode.Identity.Secrets.Abstractions/KeyTypes/ISecretKeyTypeDescriptor.cs` and `ISecretKeyTypeCatalog.cs`
- `src/NCode.Identity.Secrets.Abstractions/SecretEncodings.cs` and `KeyMaterial/SecretKeyImportFormats.cs`
- `src/NCode.Identity.Secrets.Persistence.Abstractions/ISecretImporter.cs`
- [ADR-1050](1050-a-secret-is-generated-by-the-server-revealed-once-and-never-cached.md),
  [ADR-1067](1067-secret-key-types-form-one-catalog-and-authenticated-management-callers-discover-the-deployments-capabilities.md),
  [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md),
  [ADR-1161](1161-every-owners-public-keys-have-one-source-and-are-imported-under-one-key-policy.md),
  [ADR-1266](1266-binary-data-uses-bufferfactory-rentals-with-explicit-sensitivity-and-text-only-strict-encodings.md)
- [RFC 8705](https://www.rfc-editor.org/rfc/rfc8705), OpenID Connect Core section 9
