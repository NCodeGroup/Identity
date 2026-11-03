# 1392. ES256K is an opt-in package that registers a curve and an ECDSA algorithm as data

- **Status:** Accepted (amended by
  [ADR-1398](1398-a-key-size-selects-a-curve-only-when-no-other-curve-shares-it.md): the `ecc` key type selects a
  curve by size only when it is the one catalog curve of that size, so with secp256k1 registered a 256-bit `ecc`
  key names its curve)
- **Date:** 2026-11-03
- **Deciders:** NCode Group
- **Amends:** [ADR-1272](1272-signature-algorithms-are-fully-specified-and-bound-to-their-curve.md), the construction
  of the `ES*` algorithms; [ADR-1169](1169-every-tenant-is-provisioned-with-persisted-keys.md), how a provisioned key is
  generated and when a stored key satisfies it

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
- The `ecc` key type picks P-256 for a 256-bit key, so a key generated for `ES256K` from its key type and size alone,
  as tenant key provisioning ([ADR-1169](1169-every-tenant-is-provisioned-with-persisted-keys.md)) and the management
  API generate keys, would be a P-256 key that `ES256K` refuses, and a stored key judged by its type and size alone
  would satisfy `ES256K` on P-256. The same holds for `OKP` keys, where Ed25519 and X25519 share a size.

## Decision

1. **An ECDSA algorithm is data.** `EccSignatureAlgorithmSpecification` in `NCode.Identity.Jose.Abstractions` names an
   algorithm's `alg` code, its one `EccCurveSpecification` and its hash function, and the JOSE library serves one ECDSA
   algorithm for every registered specification. `AddJoseLibrary()` registers `ES256`, `ES384` and `ES512` the same
   way a package registers `ES256K`, as `EccSignatureAlgorithmSpecification.Es256`, `Es384` and `Es512`, so the core's
   algorithms and a package's come from one composition path, as the core's `ECDH-ES` agreement and a package's do.
   The registered specifications are validated as a whole when they are composed: building the algorithm collection
   fails when a specification is null, its code is empty, whitespace or registered twice, or its curve is not the elliptic-curve catalog's curve
   of that `crv`, compared by OID too, because a key on a curve the catalog does not hold could be neither generated,
   imported nor named. The algorithm's key size and its signature size, twice the curve's size in whole bytes, come
   from the curve. The NIST curves are defined once, as `EccCurveSpecification.P256`, `P384` and `P521`, which the
   curve catalog seeds and the core's `ES*` specifications and `ECDH-ES` agreement are bound to.

2. **An algorithm's key type names the curves it is bound to.** `AlgorithmKeyType.Curves` lists the curves the
   algorithm's keys must be on, each with its `crv` and its key size: the one curve of `P-256`, `P-384`, `P-521` or
   `secp256k1` for each `ES*` algorithm, `Ed25519` or `Ed448` for the fully-specified EdDSA algorithms, both for the
   polymorphic `EdDSA`, and the agreements' curves for `ECDH-ES`. A key is checked against them by its `crv`
   (ADR-1389 decision 2), and the ECDSA algorithm compares the key's curve by OID again when it signs or verifies.

3. **Provisioning and creation generate on that curve, and a stored key is checked by its public half.** Tenant key
   provisioning and the management API's secret creation resolve the key to generate with `TryResolveKeyGeneration`
   (ADR-1389 decision 5), so `ES256K` gets a secp256k1 key and `Ed25519` an Ed25519 key, and pass the resolved curve
   into the generation request; `ISecretValidator.ValidateCreate` returns those generation parameters, and the
   generation request carries that object whole as its `GenerationParameters`, so an endpoint generates with what was
   validated and copies no field of it. A stored key satisfies a tenant key specification only when its stored public
   half is on the specification's curve and the algorithm's `IsKeyCompatible` accepts it. Every private asymmetric secret stores that half
   ([ADR-1268](1268-a-generated-private-key-stores-its-public-key-beside-it.md)), so a secret whose public half is missing or
   cannot be read cannot be shown to fit and does not satisfy the specification, and a key is provisioned.

4. **`NCode.Identity.Jose.Secp256k1` is an opt-in package.** `AddSecp256k1()` registers the secp256k1
   `EccCurveSpecification` (OID `1.3.132.0.10`, `crv` `secp256k1`), through which the `ecc` key type generates, imports
   and publishes keys on the curve, and the `ES256K` specification. It depends only on the JOSE and Secrets
   abstractions, and it extends the JOSE library's builder, `IServiceBuilder<JoseLibrary>`, whose curve catalog and
   algorithms it adds to, as `AddEdDsa()` does, so a host cannot call it without the JOSE library
   ([ADR-1069](1069-registration-library-compile-time-marker-nesting.md)). Each is registered as an instance, once,
   with `TryAddEnumerableInstance` from `NCode.Registration`, which adds an instance unless the same instance is
   registered, because `TryAddEnumerable` keeps one registration per implementation type and would drop the
   application's other curves and specifications.

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
- A tenant can be provisioned with `ES256K`, `Ed25519` and `Ed448` keys, and, because the configured curve or size
  selects among the algorithm's curves, with `OKP` keys for the polymorphic `EdDSA` and for `ECDH-ES`.
- An application that registers an ECDSA specification registers its curve too, or building the algorithm collection
  fails.
- A P-256 key declared for `ES256K`, or a secp256k1 key declared for `ES256`, is refused when a client registers it or
  the management API creates or updates it.
- An application can contribute another ECDSA algorithm, such as one over a Brainpool curve, by registering its curve
  and its specification.
- RFC 8812 publishes no test vectors, so the package's tests verify a JWS signed by an independent implementation.

## References

- [RFC 8812](https://datatracker.ietf.org/doc/html/rfc8812), [RFC 7518 §3.4](https://datatracker.ietf.org/doc/html/rfc7518#section-3.4),
  SEC 2 §2.4.1
- [ADR-1016](1016-implementation-packages-depend-only-on-abstractions.md),
  [ADR-1068](1068-unified-extensible-elliptic-curve-catalog.md),
  [ADR-1069](1069-registration-library-compile-time-marker-nesting.md),
  [ADR-1117](1117-eddsa-clean-room-implementation-opt-in-package.md),
  [ADR-1169](1169-every-tenant-is-provisioned-with-persisted-keys.md),
  [ADR-1268](1268-a-generated-private-key-stores-its-public-key-beside-it.md),
  [ADR-1272](1272-signature-algorithms-are-fully-specified-and-bound-to-their-curve.md),
  [ADR-1389](1389-one-algorithm-name-serves-every-key-type-it-declares.md)
- Issue [#337](https://github.com/NCodeGroup/Identity/issues/337), part of [#66](https://github.com/NCodeGroup/Identity/issues/66)
- `EccSignatureAlgorithmSpecification` and `AlgorithmKeyType` in `NCode.Identity.Jose.Abstractions`,
  `DefaultAlgorithmDataSource` and `EccSignatureAlgorithm` in `NCode.Identity.Jose`, `DefaultTenantKeyPolicy` in
  `NCode.Identity.OpenId.Core`, and `NCode.Identity.Jose.Secp256k1`
