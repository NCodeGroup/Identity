# 272. Signature algorithms are fully specified and bound to their curve

- **Status:** Accepted
- **Date:** 2026-10-27
- **Deciders:** NCode Group

## Context

RFC 9864 deprecates the polymorphic JOSE `EdDSA` algorithm in favour of the fully-specified `Ed25519` and `Ed448`
algorithms, so that an `alg` value names the curve and the hash it uses. It deprecates `EdDSA` but does not prohibit it,
and the OpenID conformance suite, `jose` and the repository's own conformance runner still send `alg: EdDSA`.

RFC 7518 §3.4 binds `ES256`, `ES384` and `ES512` to P-256, P-384 and P-521. A check that compares only the key type
and size cannot enforce this: the open curve catalog ([ADR-0068](0068-unified-extensible-elliptic-curve-catalog.md)) can hold a second
256-bit curve, and a size-only check accepts it as `ES256`. `EccSecretKey.GetECCurve()` used the same shortcut and
mapped every 256-bit key to P-256.

OpenID Connect computes `at_hash`, `c_hash` and `s_hash` from the signing algorithm's hash. `Ed25519` uses SHA-512, but
`Ed448` uses SHAKE256, which .NET cannot name as a `HashAlgorithmName`.

## Decision

An algorithm states which keys it accepts and which hash it uses for token hashes, and the curve is part of the
algorithm.

- `Ed25519` and `Ed448` are registered `alg` values (RFC 9864), added as `AlgorithmCodes.DigitalSignature.Ed25519` and
  `Ed448`. The opt-in EdDSA package registers them next to `EdDSA` through one
  `EdDsaSignatureAlgorithm(string code, OkpCurveInfo? curve)`; a `null` curve is the polymorphic `EdDSA`.
- `EdDSA` stays registered, selectable and advertised, so that existing interop keeps working.
- A key's fitness for an algorithm is one virtual check, `KeyedAlgorithm.IsKeyCompatible(SecretKey)`. The default is the
  key type and size rule; `EccSignatureAlgorithm` and `EdDsaSignatureAlgorithm` also bind the curve.
  `DefaultCredentialSelector` and `DefaultDPoPProofService` call it. The DPoP rejection for an incompatible key keeps
  the words "key size" ("…'jwk' key size or curve is not supported…"), because one message serves both causes.
- The `ES*` algorithms bind their curve: `EccSignatureAlgorithm(string code, ECCurve curve, HashAlgorithmName
  hashAlgorithmName)` maps ES256 to P-256, ES384 to P-384 and ES512 to P-521. `TrySign` and `Verify` throw
  `ArgumentException` on a curve mismatch. Curves are compared by `EccCurveSpecification.IsSameNamedCurve`, the OID then
  friendly-name rule of the curve catalog, which Jose and Secrets share.
- `EccSecretKey.GetECCurve()` always reads the curve from the key material.
- Token hashes come from `SignatureAlgorithm.GetTokenHashFunction(SecretKey, out int hashSizeBytes)`. The default
  derives the hash from `HashAlgorithmName`. `Ed25519` uses SHA-512 (64 bytes) and `Ed448` uses SHAKE256 with 114 bytes
  of output (OpenID Connect issue #1125); polymorphic `EdDSA` picks by the key's curve. `HashAlgorithmName` stays
  SHA-512 for every EdDSA variant. `DefaultGetIdTokenPayloadClaimsHandler` consumes it.
- The FAPI 2.0 signing ceiling is `PS256`, `ES256`, `EdDSA` and `Ed25519`. FAPI 2.0 §5.4 permits "EdDSA (Ed25519
  variant)", which `Ed25519` states exactly. `Ed448` is not in the ceiling.

## Options considered

- **Three EdDSA classes versus one parameterized class.** Only the curve differs, so one class with a code and an
  optional curve avoids three copies of the same signing code. Chosen: one class.
- **A curve check at each call site versus `IsKeyCompatible`.** Call-site checks repeat the rule and cannot know the
  rules of a contributed algorithm. The curve is a property of the algorithm, and vocabularies are open
  ([ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md)), so the algorithm states its own rule. Chosen:
  `IsKeyCompatible`.
- **Dropping `EdDSA` versus keeping it.** RFC 9864 does not prohibit it, and removing it breaks `private_key_jwt` and
  DPoP interop with the conformance suite and common libraries. Chosen: keep it registered alongside the new codes.

## Consequences

- ES256K (RFC 8812), X25519/X448 ECDH-ES (RFC 8037 §3.2) and HPKE become possible as packages: the curve binding makes
  a safe ES256K package possible, and none of them is added here.
- A 256-bit key on a curve other than P-256 no longer signs as `ES256`.
- The FAPI ceiling lists `EdDSA` as well as `Ed25519`, and `EdDSA` is not yet constrained to Ed25519 keys under FAPI. An
  `EdDSA` plus Ed448 combination therefore remains a gap.
- Ed448 signing and token hashing need `Shake256.IsSupported`.

## References

- [ADR-0068](0068-unified-extensible-elliptic-curve-catalog.md)
- [ADR-0117](0117-eddsa-clean-room-implementation-opt-in-package.md)
- [ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md)
- RFC 9864 (fully-specified algorithms for JOSE and COSE)
- RFC 8037 (CFRG elliptic curves in JOSE)
- RFC 7518 §3.4 (ECDSA digital signatures)
- FAPI 2.0 Security Profile §5.4
- OpenID Connect issue #1125 (token hashes for Ed448)
