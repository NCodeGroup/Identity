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
([ADR-1067](1067-secret-type-capability-catalog-and-capabilities-discovery.md),
[ADR-1271](1271-a-key-type-registers-its-json-web-key-rules.md)). An
implementation package references only abstractions
([ADR-1016](1016-implementation-packages-depend-only-on-abstractions.md)), an opt-in capability extends the marker of
the library it consumes ([ADR-1069](1069-di-registration-proves-every-prerequisite-at-compile-time-through-marker-builders.md)), vocabularies are open
unless provably closed ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)), and signature algorithms are
fully specified and bound to their curve
([ADR-1272](1272-signature-algorithms-are-fully-specified-and-bound-to-their-curve.md)).

The platform ships `System.Formats.Cbor`, a CBOR reader and writer. It also ships `System.Security.Cryptography.Cose`,
which only signs and binds to platform key objects.

A COSE message comes from a peer, so its size, its nesting, its signatures and its recipients bound the work it can
demand, and its algorithm and critical headers decide what the receiver does with it.

## Decision

COSE is four opt-in packages that implement the six message structures over `System.Formats.Cbor`, with the algorithms
of the JOSE stack and the keys of the family.

1. **Two packages, an abstraction and an implementation.** `NCode.Identity.Cose.Abstractions` holds the contracts and
   the registered vocabularies; `NCode.Identity.Cose` implements them. `AddCoseLibrary()` extends
   `IServiceBuilder<JoseLibrary>` and returns the `IServiceBuilder<CoseLibrary>` that `AddCoseAlgorithm` and the CWT
   package extend. The implementation references only abstractions, and `AddIdentityServer()` composes none of it.
   `System.Formats.Cbor` is the CBOR codec; `System.Security.Cryptography.Cose` is not used. `CoseLabel` and
   `CoseValue` are the value types of the contracts, as `JsonElement` is of the JOSE contracts: their factories and
   accessors read and write only their own encoded item, so a consumer of the abstractions, `NCode.Identity.Cwt`
   included, reads a header or a claim without the implementation. `CoseKeyIdentifiers` is the one rule that names a
   `kid` as a `KeyId`; the implementation, a key resolver and a reader of a confirmation `kid` apply it alike, so it is
   a public helper of the abstractions, as `TokenHash` is of the JOSE abstractions
   ([ADR-1273](1273-the-protocol-client-validates-id-tokens-under-an-asymmetric-algorithm-policy.md)).

2. **Reading is strict and bounded.** Every reader uses `CborConformanceMode.Strict` and refuses input larger than
   `CoseOptions.MaxMessageSizeBytes` before parsing it, bytes after the root item, any indefinite-length array, map,
   byte string or text string, a map label that is neither an integer nor a text string, nesting deeper than
   `MaxNestingDepth` inside a header value, a decoded map or a COSE_Key, and more than `MaxSignatures` signatures or
   `MaxRecipients` recipients. A reader failure is a `CoseException` with a fixed message and a machine-readable
   `CoseFailureCodes` code. The limits are options validated when the host starts. Key order in a received map is not
   enforced: RFC 9052 §9 requires deterministic encoding only of the structures that are signed, authenticated or used
   as additional data, which the library builds itself, and protected headers are authenticated as the bytes received.

3. **Writing is deterministic.** Every writer produces the core deterministic encoding of RFC 8949 §4.2.1: definite
   lengths, the shortest integers, and every map ordered by the bytewise order of each label's own encoding. An empty
   protected bucket is a zero-length byte string, and a reader accepts both that and an encoded empty map, signing the
   bucket as a zero-length byte string when it holds no parameter and as received otherwise.

4. **Algorithms are registered data bound to JOSE algorithms.** A `CoseAlgorithm` descriptor carries the IANA
   identifier and name, the class (`Signature`, `Mac`, `ContentEncryption`, `KeyDistribution`), the code of the JOSE
   algorithm that implements it and, for a message authentication code, the truncation. `ICoseAlgorithmCatalog` composes
   the registered descriptors with the JOSE algorithm collection when it is built: a duplicate identifier, an empty name,
   a JOSE algorithm of the wrong class or a tag size outside 8 bytes to the full output fails the composition, and a
   descriptor whose JOSE algorithm is not registered is left out, as EdDSA is without the EdDSA package. No
   cryptography is duplicated. `AddCoseLibrary()` registers ES256, ES384, ES512, ESP256, ESP384, ESP512, ES256K, EdDSA,
   Ed25519, Ed448, PS256, PS384, PS512, ML-DSA-44, ML-DSA-65, ML-DSA-87, HMAC 256/256, HMAC 384/384, HMAC 512/512,
   A128GCM, A192GCM, A256GCM, direct, A128KW, A192KW, A256KW, RSAES-OAEP w/ SHA-256 and RSAES-OAEP w/ SHA-512. HMAC
   256/64 (a 64-bit tag), RSAES-OAEP with SHA-1 and RSASSA-PKCS1-v1_5 (registered for WebAuthn interoperability) are
   offered as descriptors and registered with `AddCoseAlgorithm` by an application that needs them. The class of an
   algorithm (`CoseAlgorithmKind`) and the type of a message (`CoseMessageType`) are closed enumerations: RFC 9052
   defines exactly four algorithm classes and six message structures, each with processing in this library, so a new
   one needs a change to the processing as well as to the enumeration.

5. **Header rules.** A header value is an encoded CBOR item (`CoseValue`) under an integer or text `CoseLabel`, in a
   protected and an unprotected bucket, and a label in both buckets is refused. The known parameters have their defined
   shapes. `crit` must be protected, non-empty and list protected labels, and a label that neither the library nor the
   caller's understood list names refuses the message. `Partial IV` is refused, because it needs a Base IV that the
   family's keys do not carry. The algorithm of a body or a signature must be in the protected bucket and is checked
   against the caller's permitted list before the catalog is consulted and before any key is selected. The protected
   bucket of a recipient is empty, a recipient carrying recipients of its own is refused, and a `COSE_Sign` body carries
   no algorithm. The serializer owns `alg`, `IV` and `Partial IV` on writing.

6. **Messages.** `ICoseSerializer` encodes `COSE_Sign1`, `COSE_Sign`, `COSE_Mac0`, `COSE_Mac`, `COSE_Encrypt0` and
   `COSE_Encrypt` with external additional data, detached content and an optional tag, and verifies or decrypts them.
   `Inspect` returns the type and headers only, so content is readable only from `Verify` or `Decrypt` once the
   signature, the authentication code or the authentication tag has verified. A message without a tag is refused unless
   the caller names its type. Signatures are produced and verified by the JOSE signature algorithm over the
   `Sig_structure`. A message authentication code is computed by the JOSE HMAC over the `MAC_structure`, truncated to the
   descriptor's tag size, and compared in fixed time. Content is encrypted with AES-GCM from the JOSE library, the
   `Enc_structure` is the additional data, and the initialization vector is random and written as an `IV` header. The
   encoders return the message as a new byte array, and `ICoseKeyConverter` returns a `COSE_Key` or `COSE_KeySet` the
   same way: the output is non-secret wire data that the caller keeps or sends whole, as `IJoseEncoder.Encode` returns a
   compact token as a string.

7. **Keys are selected by what the caller supplies.** The keys a reader tries are those that are not expired (allowing
   for clock skew), are intended for the operation, are not restricted to another algorithm and suit the algorithm. When
   a layer names a `kid`, keys with that identifier are tried first, then keys with none, and a key with another
   identifier is never tried. A `kid` is an opaque byte string, and keys are matched by its bytes. `CoseKeyIdentifiers`
   names it as a key's text `KeyId`: the UTF-8 text when the bytes are UTF-8 text that does not begin with `h'`, and
   otherwise their CBOR diagnostic notation (RFC 8949 §8), such as `h'a1b2c3d4e5f60718'`; a `KeyId` in that notation is
   written as the bytes it names, and any other `KeyId` as its UTF-8 text. When no key is a candidate, the failure is
   `key_not_found`; when every candidate fails, it is `invalid_signature` or `decryption_failed`. A `COSE_Sign` verifies when one signature whose algorithm is permitted and available
   verifies.

8. **Recipients.** The key distribution algorithm of a recipient is in its unprotected bucket. A direct recipient is
   the only recipient: its key is the content key, or the message authentication code key. A key wrap or key transport
   recipient carries the content key wrapped by the JOSE algorithm; the content key is random, held in a sensitive
   pooled buffer, and unwrapped with each candidate key of each recipient whose algorithm the caller permits. None of
   the registered key distribution algorithms uses recipient context data.

9. **COSE_Key maps onto JSON Web Keys.** Key types and curves are registered descriptors
   (`CoseKeyTypeDescriptor`, `CoseEllipticCurve`) that name the JSON Web Key type, member and curve each COSE label and
   value maps onto, so a COSE_Key is imported through the registered secret key types and reads every key type the
   family serves: `OKP` (Ed25519, Ed448, X25519, X448), `EC2` (P-256, P-384, P-521, secp256k1), `RSA` and `AKP`
   (ML-DSA). `ICoseKeyConverter` reads and writes public keys only: a COSE_Key that carries any private parameter, a
   symmetric key or a Base IV is refused, as is a key whose `key_ops` is empty or combines signature and encryption
   operations; otherwise its `key_ops` set the key's use. A key with no public form is never written. A COSE_KeySet skips a key of an
   unregistered key type, curve or algorithm and refuses any other failure. The forms a parameter value takes
   (`CoseKeyParameterEncoding`) are a closed enumeration, because a JSON Web Key member is base64url bytes or text.
   A COSE_Key's `kid` becomes the key's `KeyId` by the same rule as a message `kid`, so a binary identifier reads and
   writes back unchanged.

10. **Not implemented here.** Countersignatures (RFC 9338), ECDH-ES and ECDH-SS recipients with HKDF, AES-CCM,
    ChaCha20/Poly1305, AES-MAC, HKDF key derivation, X.509 header processing (RFC 9360), CWT claims in headers
    (RFC 9597) and Base IV / Partial IV each need a JOSE algorithm the family lacks or a decision of their own.
    Their header labels are carried as unprocessed values, and a countersignature label under `crit` refuses the
    message.

11. **The packages hold the event-ID bands** `22000`–`22999` for `NCode.Identity.Cose` and `23000`–`23999` for
    `NCode.Identity.Cwt` ([ADR-1410](1410-a-cwt-is-validated-fail-closed-and-its-claims-are-read-only-after-its-cose-layer-verifies.md)).
    Neither package logs; the bands are reserved.

## Options considered

- **`System.Security.Cryptography.Cose`.** Rejected: it signs only, with no message authentication code, no encryption,
  no EdDSA and no COSE_Key, and it binds to platform key objects instead of the family's `SecretKey`.
- **A hand-written CBOR codec.** Rejected: the platform codec exists, is maintained and is strict on request.
- **COSE-specific algorithm classes.** Rejected: they would duplicate the JOSE cryptography and drift from it.
- **An API that parses a message and exposes its unverified content.** Rejected: content is readable only from
  `Verify` or `Decrypt`, and `Inspect` gives headers only.
- **`CborConformanceMode.Canonical` for writing.** Rejected: it orders map keys by length first (RFC 7049 §3.9), not by
  the bytewise order of RFC 8949 §4.2.1.
- **Enforcing key order on received maps.** Rejected: RFC 9052 §9 does not require it and the protected bytes are signed
  as received.
- **Accepting an unprotected `alg` for interoperability.** Rejected: RFC 9052 §3.1 requires `alg` to be authenticated
  where it can be.
- **Registering HMAC 256/64, RSAES-OAEP with SHA-1 and RSASSA-PKCS1-v1_5 by default.** Rejected: each is weaker than the
  default set, and an application registers one by name.
- **Moving the CBOR accessors and the key identifier rule into the implementation.** Rejected:
  `NCode.Identity.Cwt` references abstractions only and reads its claims through `CoseValue`, and a key resolver would
  need the implementation to name a `kid`.
- **A single package.** Rejected: CWT and any other consumer of COSE reference the abstractions without the
  implementation.
- **Refusing a `kid` that is not UTF-8 text.** Rejected: the example `kid` of RFC 8747 §3.4 is binary, and issuers of
  other COSE-based credentials use binary identifiers, so one such key would fail a whole COSE_KeySet.
- **Naming every `kid` in base64url or hexadecimal.** Rejected: a key named `key-1` would no longer write the UTF-8
  `kid` that RFC 8392 examples and keys shared with JOSE use.
- **A byte-valued `kid` on `KeyMetadata`.** Rejected: the Secrets abstraction would carry two identifiers for one key,
  and a key store is still looked up by a text name.

## Consequences

- An application signs, authenticates and encrypts COSE with every key type and algorithm the JOSE stack serves,
  including EdDSA and ES256K once their packages are registered and ML-DSA where the platform has it.
- A COSE algorithm that maps onto a JOSE algorithm is one registered descriptor; a COSE algorithm the JOSE stack lacks
  (AES-CCM, ChaCha20/Poly1305, ECDH with HKDF) needs that algorithm first.
- A message that violates a rule is refused with a `CoseFailureCodes` code and a fixed message that never carries the
  message bytes.
- `AddCoseLibrary()` can be called once per service collection, like the other library builders.

## References

- [RFC 9052](https://datatracker.ietf.org/doc/html/rfc9052), [RFC 9053](https://datatracker.ietf.org/doc/html/rfc9053),
  [RFC 8949](https://datatracker.ietf.org/doc/html/rfc8949), [RFC 8230](https://datatracker.ietf.org/doc/html/rfc8230),
  [RFC 8812](https://datatracker.ietf.org/doc/html/rfc8812), [RFC 9864](https://datatracker.ietf.org/doc/html/rfc9864),
  [RFC 9964](https://datatracker.ietf.org/doc/html/rfc9964), [RFC 9338](https://datatracker.ietf.org/doc/html/rfc9338),
  [RFC 9360](https://datatracker.ietf.org/doc/html/rfc9360), [RFC 9596](https://datatracker.ietf.org/doc/html/rfc9596)
- [ADR-1016](1016-implementation-packages-depend-only-on-abstractions.md),
  [ADR-1067](1067-secret-type-capability-catalog-and-capabilities-discovery.md),
  [ADR-1069](1069-di-registration-proves-every-prerequisite-at-compile-time-through-marker-builders.md),
  [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md),
  [ADR-1271](1271-a-key-type-registers-its-json-web-key-rules.md),
  [ADR-1272](1272-signature-algorithms-are-fully-specified-and-bound-to-their-curve.md),
  [ADR-1273](1273-the-protocol-client-validates-id-tokens-under-an-asymmetric-algorithm-policy.md),
  [ADR-1410](1410-a-cwt-is-validated-fail-closed-and-its-claims-are-read-only-after-its-cose-layer-verifies.md)
- Issue [#76](https://github.com/NCodeGroup/identity/issues/76)
- `NCode.Identity.Cose.Abstractions`: `ICoseSerializer`, `CoseAlgorithms`, `CoseKeyTypeDescriptors`,
  `CoseKeyIdentifiers`;
  `NCode.Identity.Cose`: `DefaultCoseSerializer`, `DefaultCoseAlgorithmCatalog`, `DefaultCoseKeyConverter`,
  `CborGuard`, `CborMapWriter`
