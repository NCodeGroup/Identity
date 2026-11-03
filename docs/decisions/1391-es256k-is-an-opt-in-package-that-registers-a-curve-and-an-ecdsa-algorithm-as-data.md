# 1391. ES256K is an opt-in package that registers a curve and an ECDSA algorithm as data

- **Status:** Accepted
- **Date:** 2026-11-03
- **Deciders:** NCode Group
- **Amends:** [ADR-1272](1272-signature-algorithms-are-fully-specified-and-bound-to-their-curve.md), the construction
  of the `ES*` algorithms; [ADR-1169](1169-every-tenant-is-provisioned-with-persisted-keys.md), how a provisioned key is
  generated and when a stored key satisfies it; [ADR-1390](1390-x25519-and-x448-agree-keys-beside-the-okp-signing-keys-and-a-curves-use-separates-them.md),
  its consequence that no `OKP` key can be provisioned

## Context

[RFC 8812](https://datatracker.ietf.org/doc/html/rfc8812) registers the JWS algorithm `ES256K`, ECDSA over secp256k1
with SHA-256, and the JWK `crv` value `secp256k1`. [ADR-1272](1272-signature-algorithms-are-fully-specified-and-bound-to-their-curve.md)
binds every `ES*` algorithm to its curve, which is what makes `ES256K` safe to add: secp256k1 and P-256 are both
256-bit curves, so a check of key type and size alone would let a P-256 key sign as `ES256K` and a secp256k1 key sign
as `ES256`.

Three facts shape how it is added:

- The ECDSA signature algorithm lives, `internal`, in `NCode.Identity.Jose`, and an implementation package references
  only `*.Abstractions` packages ([ADR-1016](1016-implementation-packages-depend-only-on-abstractions.md)). An opt-in
  package can therefore neither construct it nor reference the JOSE library.
- .NET's ECDSA serves secp256k1 only where the platform's cryptography does: OpenSSL on Linux and CNG on Windows
  support it, Apple's platform cryptography on macOS does not, and `ECDsa.Create` throws there.
- Tenant key provisioning ([ADR-1169](1169-every-tenant-is-provisioned-with-persisted-keys.md)) generates a key from
  an algorithm's key type and size. The `ecc` key type picks P-256 for 256 bits, so provisioning `ES256K` would store a
  P-256 key that `ES256K` refuses. Provisioning also counts a stored key as satisfying an algorithm by its type and size,
  so an unbound P-256 key would satisfy `ES256K`. [ADR-1390](1390-x25519-and-x448-agree-keys-beside-the-okp-signing-keys-and-a-curves-use-separates-them.md)
  records the same gap for `OKP` keys, where Ed25519 and X25519 share a size.

## Decision

1. **An ECDSA algorithm is data.** `EccSignatureAlgorithmSpecification` in `NCode.Identity.Jose.Abstractions` names an
   algorithm's `alg` code, its one `EccCurveSpecification` and its hash function. The JOSE library serves `ES256`,
   `ES384` and `ES512` from its own specifications and one ECDSA algorithm for every registered specification, and
   startup fails when a code is empty or registered twice, a built-in one included. The algorithm's key size and its
   signature size, twice the curve's size in whole bytes, come from the curve.

2. **An algorithm's key type names the curve it is bound to.** `AlgorithmKeyType.Curve` is the `crv` of the one curve
   the algorithm's keys must be on: `P-256`, `P-384`, `P-521` and `secp256k1` for the `ES*` algorithms, `Ed25519` and
   `Ed448` for the fully-specified EdDSA algorithms, and `null` for an algorithm that accepts more than one curve, such as
   the polymorphic `EdDSA` and `ECDH-ES`. The algorithm's own `IsKeyCompatible` stays the check of a key.

3. **Tenant provisioning generates on that curve and checks the stored key.** A specification carries the key type's
   curve into the generation request, so `ES256K` gets a secp256k1 key and `Ed25519` an Ed25519 key. A stored key with a
   stored public half satisfies a specification only when the algorithm's `IsKeyCompatible` accepts that public key; a
   public half that cannot be read does not satisfy it, and a key held without one is trusted as before.

4. **`NCode.Identity.Jose.Secp256k1` is an opt-in package.** `AddSecp256k1()` registers the secp256k1
   `EccCurveSpecification` (OID `1.3.132.0.10`, `crv` `secp256k1`), through which the `ecc` key type generates, imports
   and publishes keys on the curve, and the `ES256K` specification. It depends only on the JOSE and Secrets
   abstractions. Each is registered as an instance, once, because `TryAddEnumerable` keeps one registration per
   implementation type and would drop the application's other curves.

5. **Registration is gated on the platform.** `AddSecp256k1()` first probes whether the platform's cryptography can
   create a secp256k1 key and sign and verify with it, and registers nothing where it cannot, so discovery, the JWKS and
   the capabilities document never offer a curve or an algorithm the host cannot use. P-256 stays the curve the `ecc`
   key type selects by size.

## Options considered

- **The package implements ECDSA itself.** Rejected: it would copy the ECDSA signing, verification and curve check of
  the JOSE library, and the two copies could drift. A specification keeps one implementation.
- **Making the ECDSA algorithm public.** Rejected: the package would then reference the JOSE library, which
  [ADR-1016](1016-implementation-packages-depend-only-on-abstractions.md) forbids, or the class would move into an
  abstractions package, which holds no logic.
- **Putting `ES256K` in the core JOSE library.** Rejected: secp256k1 has a narrow audience, no OpenID or FAPI profile
  asks for it, and the core library would carry a platform-dependent algorithm. It stays opt-in, as
  [ADR-1117](1117-eddsa-clean-room-implementation-opt-in-package.md) keeps EdDSA.
- **Gating when the algorithms are listed instead of at registration.** Rejected: the curve would still reach the
  curve catalog, so a JWK on secp256k1 would be accepted and then fail where it is used.
- **Refusing to provision a curve-bound algorithm.** Rejected: the curve is known from the algorithm, so provisioning
  can generate the right key instead of failing.

## Consequences

- With `AddSecp256k1()` on a platform that supports the curve, `ES256K` signs and verifies, is advertised with the
  other asymmetric signing algorithms, and `ecc` keys on secp256k1 are imported and exported as JWKs with
  `crv` `secp256k1`. A P-256 key never signs as `ES256K`, and a secp256k1 key never signs as `ES256`.
- On macOS the package registers nothing, and its platform-dependent tests are skipped.
- A tenant can be provisioned with `ES256K`, `Ed25519` and `Ed448` keys. Provisioning an `OKP` key for the polymorphic
  `EdDSA` or for `ECDH-ES` still needs a curve that the algorithm does not name, so it still fails.
- An application can contribute another ECDSA algorithm, such as one over a Brainpool curve, by registering its curve
  and its specification.
- RFC 8812 publishes no test vectors, so the package's tests verify a JWS signed by an independent implementation.

## References

- [RFC 8812](https://datatracker.ietf.org/doc/html/rfc8812), [RFC 7518 §3.4](https://datatracker.ietf.org/doc/html/rfc7518#section-3.4),
  SEC 2 §2.4.1
- [ADR-1016](1016-implementation-packages-depend-only-on-abstractions.md),
  [ADR-1068](1068-unified-extensible-elliptic-curve-catalog.md),
  [ADR-1117](1117-eddsa-clean-room-implementation-opt-in-package.md),
  [ADR-1169](1169-every-tenant-is-provisioned-with-persisted-keys.md),
  [ADR-1272](1272-signature-algorithms-are-fully-specified-and-bound-to-their-curve.md),
  [ADR-1390](1390-x25519-and-x448-agree-keys-beside-the-okp-signing-keys-and-a-curves-use-separates-them.md)
- Issue [#337](https://github.com/NCodeGroup/Identity/issues/337), part of [#66](https://github.com/NCodeGroup/Identity/issues/66)
- `EccSignatureAlgorithmSpecification` and `AlgorithmKeyType` in `NCode.Identity.Jose.Abstractions`,
  `DefaultAlgorithmDataSource` and `EccSignatureAlgorithm` in `NCode.Identity.Jose`, `DefaultTenantKeyPolicy` in
  `NCode.Identity.OpenId.Core`, and `NCode.Identity.Jose.Secp256k1`
