# 1409. COSE messages are an opt-in package family over System.Formats.Cbor and the family's keys and algorithms

- **Status:** Accepted
- **Date:** 2026-11-04
- **Deciders:** NCode Group

## Context

CBOR Object Signing and Encryption (COSE, [RFC 9052](https://datatracker.ietf.org/doc/html/rfc9052) structures,
[RFC 9053](https://datatracker.ietf.org/doc/html/rfc9053) algorithms) is JOSE's model on CBOR. It is the signing and
encryption layer of CBOR Web Tokens ([RFC 8392](https://datatracker.ietf.org/doc/html/rfc8392)), of ISO mdoc and mDL
(18013-5) and of EUDI wallet credentials, and its messages are small enough for constrained devices.

The family already has a JOSE algorithm set and a `SecretKey` model with registered key types
([ADR-1067](1067-secret-key-types-form-one-catalog-and-authenticated-management-callers-discover-the-deployments-capabilities.md),
[ADR-1119](1119-a-di-free-jwk-package-models-keys-sets-and-thumbprints-and-key-types-register-their-rules.md)). An
implementation package references only abstractions
([ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md)), an opt-in capability extends the marker of
the library it consumes ([ADR-1069](1069-di-registration-proves-every-prerequisite-at-compile-time-through-marker-builders.md)), vocabularies are open
unless provably closed ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)), and signature algorithms are
fully specified and bound to their curve
([ADR-1392](1392-ecdsa-and-eddsa-algorithms-are-fully-specified-and-bound-to-their-curve-and-es256k-is-opt-in.md)).

The platform ships `System.Formats.Cbor`, a CBOR reader and writer. It also ships `System.Security.Cryptography.Cose`,
which only signs and binds to platform key objects.

A COSE message comes from a peer, so its size, its nesting, its signatures and its recipients bound the work it can
demand, and its algorithm and critical headers decide what the receiver does with it.

## Decision

COSE is opt-in packages that implement the six message structures over `System.Formats.Cbor`, with the algorithms of the
JOSE stack and the keys of the family.

1. **Two packages.** `NCode.Identity.Cose.Abstractions` holds the contracts, the registered vocabularies, the value
   types `CoseLabel` and `CoseValue` and the public `CoseKeyIdentifiers`; `NCode.Identity.Cose` implements them.
   `AddCoseLibrary()` extends `IServiceBuilder<JoseLibrary>` and returns the `IServiceBuilder<CoseLibrary>` that
   `AddCoseAlgorithm` and the CWT package extend. `AddIdentityServer()` composes none of it. `System.Formats.Cbor` is the
   codec; `System.Security.Cryptography.Cose` is not used. `CoseLabel` and `CoseValue` read and write only their own
   encoded item, as `JsonElement` does for the JOSE contracts, so a consumer of the abstractions, `NCode.Identity.Cwt`
   included, reads a header or a claim without the implementation. `CoseKeyIdentifiers` is the one rule that names a
   `kid` as a `KeyId`, applied alike by the implementation, a key resolver and a reader of a confirmation `kid`.

2. **Reading is strict and bounded.** Every reader uses `CborConformanceMode.Strict` and refuses input larger than
   `CoseOptions.MaxMessageSizeBytes` before parsing it, bytes after the root item, any indefinite length, a map label
   that is neither an integer nor a text string, nesting deeper than `MaxNestingDepth`, and more than `MaxSignatures`
   signatures or `MaxRecipients` recipients. The limits are validated when the host starts. The order of a received map
   is not enforced: RFC 9052 §9 requires deterministic encoding only of structures the library builds, and protected
   headers are authenticated as received. A failure is a `CoseException` with a fixed message and a `CoseFailureCodes`
   code, and it never carries the message bytes.

3. **Writing is deterministic.** Every writer produces the core deterministic encoding of RFC 8949 §4.2.1: definite
   lengths, the shortest integers and maps ordered by the bytewise order of each label's encoding. An empty protected
   bucket is a zero-length byte string; a reader accepts that and an encoded empty map.

4. **Algorithms are registered data bound to JOSE algorithms.** A `CoseAlgorithm` descriptor carries the IANA
   identifier, the name, the class, the JOSE algorithm code and, for a MAC, the truncation. `ICoseAlgorithmCatalog`
   composes the descriptors with the JOSE algorithm collection: a duplicate identifier, an empty name, a JOSE algorithm
   of the wrong class or a tag size outside 8 bytes to the full output fails the composition, and a descriptor whose JOSE
   algorithm is absent is skipped, as EdDSA is without its package. `AddCoseLibrary()` registers ES256 to ES512, ESP256
   to ESP512, ES256K, EdDSA, Ed25519, Ed448, PS256 to PS512, ML-DSA-44, -65 and -87, HMAC 256/256, 384/384 and 512/512,
   A128GCM to A256GCM, direct, A128KW to A256KW and RSAES-OAEP with SHA-256 and with SHA-512. HMAC 256/64, RSAES-OAEP
   with SHA-1 and RSASSA-PKCS1-v1_5 are descriptors an application adds with `AddCoseAlgorithm`. `CoseAlgorithmKind`,
   `CoseMessageType` and `CoseKeyParameterEncoding` are closed enumerations: RFC 9052 fixes the algorithm classes and the
   message structures, each with processing in this library, and a JWK member is bytes or text.

5. **Header rules.** A label in both buckets is refused. `crit` must be protected, non-empty and understood. `Partial
   IV` is refused, because it needs a Base IV that the family's keys do not carry. `alg` must be protected and is checked
   against the caller's permitted list before the catalog is consulted and before any key is selected. A recipient's
   protected bucket is empty and a recipient with recipients of its own is refused. `crit` lists only protected labels, and
   a `COSE_Sign` body carries no `alg`, because each signature names its own. On writing, the serializer sets `alg` and
   `IV` itself and refuses a caller's header that sets `alg`, `IV` or `Partial IV`.

6. **Messages.** `ICoseSerializer` handles `COSE_Sign1`, `COSE_Sign`, `COSE_Mac0`, `COSE_Mac`, `COSE_Encrypt0` and
   `COSE_Encrypt` with external additional data, detached content and an optional tag. `EncodeSign1Async` and
   `EncodeSignAsync` sign asynchronously
   ([ADR-1472](1472-signing-and-content-key-unwrapping-are-asynchronous-from-the-jose-algorithm-to-every-caller.md));
   MAC and encryption are synchronous. Content is readable only from `Verify` or `Decrypt`, and `Inspect` returns headers
   only. A MAC is the JOSE HMAC truncated to the descriptor's tag size and compared in fixed time. Encryption is the JOSE
   AES-GCM with a random `IV`. Each encoder returns a new `byte[]`: non-secret wire data the caller keeps or sends whole.
   A message without a tag is refused unless the caller names its type (`UntaggedMessageType`).

7. **Keys are selected by what the caller supplies.** A reader tries the keys that are unexpired (with skew), meant for
   the operation and fit for the algorithm. A named `kid` tries matching keys first, then keys without one, never others.
   `CoseKeyIdentifiers` maps the bytes to a `KeyId` as UTF-8 text, or as diagnostic `h'...'` notation (RFC 8949 §8) when
   they are not UTF-8 text or begin with `h'`, and maps it back. The failures are `key_not_found`, `invalid_signature`
   and `decryption_failed`. A `COSE_Sign` verifies when one signature whose algorithm is permitted and available
   verifies.

8. **Recipients.** A direct recipient is the only recipient. A key wrap or key transport recipient unwraps a random
   content key held in a sensitive pooled buffer. No registered key distribution algorithm uses recipient context data.

9. **COSE_Key maps onto JSON Web Keys.** `ICoseKeyConverter` maps `COSE_Key` to a JWK through `CoseKeyTypeDescriptor` and
   `CoseEllipticCurve` for OKP, EC2, RSA and AKP. It handles public keys only: a private parameter, a symmetric key or a
   Base IV is refused, as are empty or mixed `key_ops`. A `COSE_KeySet` skips an unregistered key type, curve or
   algorithm.

10. **Not implemented.** Countersignatures, ECDH-ES and ECDH-SS with HKDF, AES-CCM, ChaCha20/Poly1305, AES-MAC, HKDF,
    X.509 headers and CWT claims in headers. Their labels are carried unprocessed, and a countersignature label under
    `crit` refuses the message.

11. **Event IDs.** The Cose package holds the band 22000 to 22999 and the Cwt package 23000 to 23999
    ([ADR-1410](1410-a-cwt-is-validated-fail-closed-and-its-claims-are-read-only-after-its-cose-layer-verifies.md)); the
    bands are reserved.

## Options considered

- **`System.Security.Cryptography.Cose`.** Rejected: it signs only, with no MAC, no encryption, no EdDSA and no
  COSE_Key, and it binds to platform key objects instead of `SecretKey`.
- **A hand-written CBOR codec.** Rejected: the platform codec exists and is strict on request.
- **COSE-specific cryptography.** Rejected: it duplicates the JOSE algorithms and drifts from them.
- **Exposing unverified content.** Rejected: content is readable only after verification.
- **`CborConformanceMode.Canonical` for writing.** Rejected: it orders keys by length first, not bytewise.
- **Enforcing received map order.** Rejected: RFC 9052 does not require it and protected bytes are signed as received.
- **An unprotected `alg`.** Rejected: RFC 9052 §3.1 requires `alg` to be authenticated where it can be.
- **Weak algorithms by default.** Rejected: HMAC 256/64, SHA-1 OAEP and PKCS1-v1_5 are each registered by name.
- **CBOR accessors and the `kid` rule in the implementation.** Rejected: `NCode.Identity.Cwt` references abstractions only.
- **A single package.** Rejected: CWT and other consumers reference the abstractions alone.
- **Refusing binary `kid`s, or naming every `kid` in base64url.** Rejected: RFC 8747's example `kid` is binary, and a
  text `kid` such as `key-1` must stay UTF-8.
- **A byte-valued `kid` on `KeyMetadata`.** Rejected: one key would carry two identifiers.

The chosen design is the registered, bounded implementation over `System.Formats.Cbor` described above.

## Consequences

- An application signs, authenticates and encrypts COSE with every key type and algorithm the JOSE stack serves,
  including EdDSA and ES256K once their packages are registered and ML-DSA where the platform has it.
- A COSE algorithm that maps onto a JOSE algorithm is one registered descriptor; one the JOSE stack lacks needs that
  algorithm first.
- A message that violates a rule is refused with a `CoseFailureCodes` code and a fixed message.
- `AddCoseLibrary()` can be called once per service collection.

## References

- [RFC 9052](https://datatracker.ietf.org/doc/html/rfc9052), [RFC 9053](https://datatracker.ietf.org/doc/html/rfc9053),
  [RFC 8949](https://datatracker.ietf.org/doc/html/rfc8949), [RFC 8230](https://datatracker.ietf.org/doc/html/rfc8230),
  [RFC 8812](https://datatracker.ietf.org/doc/html/rfc8812), [RFC 9864](https://datatracker.ietf.org/doc/html/rfc9864),
  [RFC 9964](https://datatracker.ietf.org/doc/html/rfc9964), [RFC 9338](https://datatracker.ietf.org/doc/html/rfc9338),
  [RFC 9360](https://datatracker.ietf.org/doc/html/rfc9360), [RFC 9596](https://datatracker.ietf.org/doc/html/rfc9596)
- [ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md),
  [ADR-1067](1067-secret-key-types-form-one-catalog-and-authenticated-management-callers-discover-the-deployments-capabilities.md),
  [ADR-1069](1069-di-registration-proves-every-prerequisite-at-compile-time-through-marker-builders.md),
  [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md),
  [ADR-1119](1119-a-di-free-jwk-package-models-keys-sets-and-thumbprints-and-key-types-register-their-rules.md),
  [ADR-1392](1392-ecdsa-and-eddsa-algorithms-are-fully-specified-and-bound-to-their-curve-and-es256k-is-opt-in.md),
  [ADR-1472](1472-signing-and-content-key-unwrapping-are-asynchronous-from-the-jose-algorithm-to-every-caller.md),
  [ADR-1410](1410-a-cwt-is-validated-fail-closed-and-its-claims-are-read-only-after-its-cose-layer-verifies.md)
- Issue [#76](https://github.com/NCodeGroup/identity/issues/76)
- `NCode.Identity.Cose.Abstractions`: `ICoseSerializer`, `CoseAlgorithms`, `CoseKeyTypeDescriptors`,
  `CoseKeyIdentifiers`;
  `NCode.Identity.Cose`: `DefaultCoseSerializer`, `DefaultCoseAlgorithmCatalog`, `DefaultCoseKeyConverter`,
  `CborGuard`, `CborMapWriter`
