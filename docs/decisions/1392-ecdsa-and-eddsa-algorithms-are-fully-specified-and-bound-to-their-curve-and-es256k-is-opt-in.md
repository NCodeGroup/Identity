# 1392. ECDSA and EdDSA algorithms are fully specified and bound to their curve, and ES256K is opt-in

- **Status:** Accepted
- **Date:** 2026-11-03
- **Deciders:** NCode Group

## Context

RFC 9864 deprecates the polymorphic JOSE `EdDSA` algorithm in favour of the fully-specified `Ed25519` and `Ed448`
algorithms, so that an `alg` value names the curve and the hash it uses. It deprecates `EdDSA` but does not prohibit it,
and the OpenID conformance suite, `jose` and the repository's own conformance runner still send `alg: EdDSA`.

RFC 7518 §3.4 binds `ES256`, `ES384` and `ES512` to P-256, P-384 and P-521. A check that compares only the key type and
size cannot enforce this: the open curve catalog
([ADR-1068](1068-elliptic-curves-are-one-extensible-catalog-and-an-imported-point-is-checked-against-its-curve.md))
can hold a second 256-bit curve, and a size-only check accepts it as `ES256`. RFC 8812 registers `ES256K`, ECDSA over
secp256k1 with SHA-256, and the JWK `crv` value `secp256k1`. secp256k1 and P-256 are both 256-bit curves, so without a
curve binding a P-256 key could sign as `ES256K` and a secp256k1 key as `ES256`. The binding is what makes `ES256K`
safe to add.

OpenID Connect computes `at_hash`, `c_hash` and `s_hash` from the signing algorithm's hash. `Ed25519` uses SHA-512, but
`Ed448` uses SHAKE256, which .NET cannot name as a `HashAlgorithmName`.

Three more facts shape how `ES256K` is added:

- The ECDSA signature algorithm is `internal` to `NCode.Identity.Jose`, and an implementation package references only
  `*.Abstractions` packages
  ([ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md)).
  An opt-in package can therefore neither construct it nor reference the JOSE library.
- .NET's ECDSA serves secp256k1 only where the platform's cryptography does: OpenSSL on Linux and CNG on Windows
  support it, Apple's platform cryptography on macOS does not, and `ECDsa.Create` throws there.
- The ECDSA signature algorithms must come from one composition path, so that the core's algorithms and a package's are
  registered the same way.

## Decision

An algorithm states which keys it accepts and which hash it uses for token hashes, and the curve is part of the
algorithm.

1. **A key's fitness for an algorithm is one check.** `KeyedAlgorithm.IsKeyCompatible` is evaluated over the curves of
   the algorithm's `AlgorithmKeyType.Curves`
   ([ADR-1389](1389-one-algorithm-name-serves-every-key-type-and-curve-it-declares-and-ecdh-es-agreements-are-registered.md)).
   `DefaultCredentialSelector`, `DefaultDPoPProofService` and `ValidateKeyAlgorithm` call it. The DPoP rejection for an
   incompatible key reads "The DPoP proof 'jwk' key size or curve is not supported by the '{alg}' algorithm.", because
   one message serves both causes.

2. **An ECDSA algorithm is data.** `EccSignatureAlgorithmSpecification` in `NCode.Identity.Jose.Abstractions` names the
   `alg` code, one `EccCurveSpecification` and the hash function. The JOSE library serves one internal
   `EccSignatureAlgorithm` for every registered specification, and takes the key size and the signature size (twice the
   curve's size in whole bytes) from the curve. `AddJoseLibrary()` registers `EccSignatureAlgorithmSpecification.Es256`,
   `Es384` and `Es512`, bound to `EccCurveSpecification.P256`, `P384` and `P521`. The NIST curves are defined once, and
   they also seed the curve catalog and the ECDH-ES agreement.

3. **The specifications are validated as a whole when composed.** Building the algorithm collection fails when a
   specification is null, its code is empty, whitespace or registered twice, or its curve is not the catalog's curve of
   that `crv`, compared by OID too, because a key on a curve the catalog does not hold could be neither generated,
   imported nor named.

4. **ECDSA signing and verification bind the curve.** Sign and verify throw `ArgumentException` on a curve mismatch,
   comparing curves with `EccCurveSpecification.IsSameNamedCurve` (OID, then friendly name). `EccSecretKey.GetECCurve()`
   reads the curve from the key material.

5. **EdDSA has fully-specified codes.** `Ed25519` and `Ed448` (`AlgorithmCodes.DigitalSignature`) are registered beside
   the polymorphic `EdDSA`. `EdDSA` stays registered, selectable and advertised for interoperability. The EdDSA package
   itself is described in
   [ADR-1390](1390-the-opt-in-eddsa-package-implements-ed25519-ed448-x25519-and-x448-clean-room-over-okp-keys.md).

6. **Token hashes come from the algorithm.** `SignatureAlgorithm.GetTokenHashFunction(SecretKey, out int hashSizeBytes)`
   returns the hash. The default derives it from `HashAlgorithmName`. `Ed25519` uses SHA-512 (64 bytes) and `Ed448` uses
   SHAKE256 with 114 bytes of output (OpenID Connect issue #1125). The polymorphic `EdDSA` picks by the key's curve.
   `HashAlgorithmName` is SHA-512 for every EdDSA variant. `DefaultGetIdTokenPayloadClaimsHandler` and `TokenHash`
   consume `GetTokenHashFunction`, so no hash is derived from an `alg` name.

7. **The FAPI 2.0 signing ceiling is `PS256`, `ES256` and `Ed25519`.** `Fapi2SecurityProfile` applies it to every JWT the
   profile governs, DPoP proofs included. FAPI 2.0 §5.4 permits "EdDSA (Ed25519 variant)", which `Ed25519` states
   exactly. The polymorphic `EdDSA`, which also accepts an `Ed448` key, and `Ed448` are outside it (issue #333).

8. **`NCode.Identity.Jose.Secp256k1` is an opt-in package.** `AddSecp256k1()` extends `IServiceBuilder<JoseLibrary>`, so
   a host cannot call it without the JOSE library
   ([ADR-1069](1069-di-registration-proves-every-prerequisite-at-compile-time-through-marker-builders.md)). It depends
   only on the JOSE and Secrets abstractions. It registers the secp256k1 `EccCurveSpecification` (OID `1.3.132.0.10`,
   `crv` `secp256k1`), through which the `ecc` key type generates, imports and publishes keys on the curve, and the
   `ES256K` specification (RFC 8812). Each is registered once as an instance with `TryAddEnumerableInstance` from
   `NCode.Identity.Registration`, because `TryAddEnumerable` keeps one registration per implementation type and would
   drop the application's other curves and specifications.

9. **Registration is gated on a platform probe.** `Secp256k1Curve.IsSupported` creates a secp256k1 key and signs and
   verifies with it. Where the probe fails, as on macOS, `AddSecp256k1()` registers nothing, so discovery, the JWKS and
   the capabilities document never offer a curve or an algorithm the host cannot use.

## Options considered

- **The package implements ECDSA itself.** Rejected: it would copy the ECDSA signing, verification and curve check of the
  JOSE library, and the two copies could drift. A specification keeps one implementation.
- **A public ECDSA class.** Rejected: the package would then reference the JOSE library, which
  [ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md)
  forbids, or the class would move into an abstractions package, which holds no logic.
- **`ES256K` in the core JOSE library.** Rejected: secp256k1 has a narrow audience, no OpenID or FAPI profile asks for
  it, and the core library would carry a platform-dependent algorithm. It stays opt-in, as EdDSA does.
- **Gating when algorithms are listed rather than at registration.** Rejected: the curve would still reach the curve
  catalog, so a JWK on secp256k1 would be accepted and then fail where it is used.
- **A curve check at each call site.** Rejected: call-site checks repeat the rule and cannot know the rules of a
  contributed algorithm. The curve is a property of the algorithm, and vocabularies are open
  ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)), so the algorithm states its own rule.
- **Three EdDSA classes.** Rejected: only the curve differs, so one parameterized class avoids three copies of the same
  signing code.
- **Dropping `EdDSA`.** Rejected: RFC 9864 does not prohibit it, and removing it breaks `private_key_jwt` and DPoP
  interop with the conformance suite and common libraries.
- **Chosen:** algorithms as data bound to their curve through `IsKeyCompatible`, `Ed25519` and `Ed448` registered beside
  `EdDSA`, and `ES256K` as an opt-in package gated on the platform.

## Consequences

- A P-256 key never signs as `ES256K`, and a secp256k1 key never signs as `ES256`.
- With `AddSecp256k1()` on a platform that supports the curve, `ES256K` signs and verifies, is advertised with the other
  asymmetric signing algorithms, and `ecc` keys on secp256k1 are imported and exported as JWKs with `crv` `secp256k1`.
  On macOS the package registers nothing, and its platform-dependent tests are skipped.
- An application contributes another ECDSA algorithm, such as one over a Brainpool curve, by registering its curve and
  its specification. An application that registers a specification registers its curve too, or building the algorithm
  collection fails.
- The FAPI 2.0 ceiling lists `Ed25519` and not the polymorphic `EdDSA`, so a FAPI 2.0 party signs with the
  fully-specified identifier, and an `EdDSA` plus Ed448 combination cannot reach a FAPI 2.0 tenant. The repository's
  conformance runner can sign with `Ed25519` or `Ed448` explicitly.
- Ed448 signing and token hashing need `Shake256.IsSupported`.
- RFC 8812 publishes no test vectors, so the package's tests verify a JWS signed by an independent implementation.

## References

- [RFC 8812](https://datatracker.ietf.org/doc/html/rfc8812), [RFC 7518 §3.4](https://datatracker.ietf.org/doc/html/rfc7518#section-3.4),
  SEC 2 §2.4.1, RFC 9864 (fully-specified algorithms for JOSE and COSE), RFC 8037, FAPI 2.0 Security Profile §5.4
- OpenID Connect issue #1125 (token hashes for Ed448)
- Issue [#337](https://github.com/NCodeGroup/identity/issues/337), part of [#66](https://github.com/NCodeGroup/identity/issues/66);
  issue [#333](https://github.com/NCodeGroup/identity/issues/333)
- [ADR-1068](1068-elliptic-curves-are-one-extensible-catalog-and-an-imported-point-is-checked-against-its-curve.md),
  [ADR-1069](1069-di-registration-proves-every-prerequisite-at-compile-time-through-marker-builders.md),
  [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md),
  [ADR-1389](1389-one-algorithm-name-serves-every-key-type-and-curve-it-declares-and-ecdh-es-agreements-are-registered.md),
  [ADR-1390](1390-the-opt-in-eddsa-package-implements-ed25519-ed448-x25519-and-x448-clean-room-over-okp-keys.md),
  [ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md)
- `EccSignatureAlgorithmSpecification` and `AlgorithmKeyType` in `NCode.Identity.Jose.Abstractions`,
  `DefaultAlgorithmDataSource` and `EccSignatureAlgorithm` in `NCode.Identity.Jose`, `Fapi2SecurityProfile`,
  `DefaultGetIdTokenPayloadClaimsHandler` and `TokenHash` in `NCode.Identity.OpenId.Core`, and
  `NCode.Identity.Jose.Secp256k1`
