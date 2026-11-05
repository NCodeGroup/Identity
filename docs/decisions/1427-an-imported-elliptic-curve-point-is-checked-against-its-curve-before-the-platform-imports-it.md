# 1427. An imported elliptic-curve point is checked against its curve before the platform imports it

- **Status:** Accepted
- **Date:** 2026-11-05
- **Deciders:** NCode Group
- **Refines:** [ADR-1068](1068-unified-extensible-elliptic-curve-catalog.md), the curve specification a catalog entry
  carries (it also holds the curve's domain)

## Context

The platform judges an elliptic-curve point differently per operating system. Windows CNG answers an off-curve secp256k1
point with `PlatformNotSupportedException`, and OpenSSL answers it with `CryptographicException`, so the same invalid
JWK public key fails with a different exception type depending on where the server runs. A peer key for `ECDH-ES` must
be on the curve as well, because an invalid-curve point is an attack on the key agreement. The curve equation is
arithmetic on public constants, so the check does not need the platform.

## Decision

1. **`EccCurveSpecification.Domain` is required.** `EccCurveDomain` lives in `NCode.Identity.Secrets.Abstractions`,
   namespace `NCode.Identity.Secrets.KeyTypes`, and holds the prime `p` and the coefficients `a` and `b` of the
   short-Weierstrass equation `y² ≡ x³ + ax + b (mod p)`. `EccCurveDomain.FromHex` builds one from constants; P-256,
   P-384, P-521 and secp256k1 use constants, so their check is platform-independent. `EccCurveDomain.FromCurve(ECCurve)`
   reads the domain of another curve the platform supports, such as brainpoolP256r1, for an application that registers
   it. A registered curve always supplies its domain.
2. **The descriptor checks the point first.** `EccSecretKeyTypeDescriptor.ParseJwkPublicKey` calls
   `EccCurveDomain.ContainsPoint(x, y)` before it imports. The point is valid when each coordinate is exactly the
   field's byte length (RFC 7518 §6.2.1.2–3), each is below `p`, and the point satisfies the curve equation. An invalid
   point throws `ArgumentException`, on every platform.
3. **The platform still imports.** The platform creates the key from the checked parameters, and a failure that is the
   platform's own, such as a curve the platform does not support, keeps its exception.

## Options considered

- **Catching `PlatformNotSupportedException` beside `CryptographicException`.** It hides a real unsupported-curve error
  as an invalid point and leaves the check defined by the platform.
- **Reading the domain from `ExportExplicitParameters` at run time.** The platform does not offer it for every curve on
  every operating system, so the check would depend on the platform again; the constants do not.

## Consequences

- An off-curve point, a coordinate of the wrong length and a coordinate at or above `p` fail with `ArgumentException` on
  every platform.
- A peer key for `ECDH-ES` is on its curve before the key agreement runs.
- A curve registered by an application supplies its domain, from constants or from `EccCurveDomain.FromCurve`.

## References

- [ADR-1068](1068-unified-extensible-elliptic-curve-catalog.md),
  [ADR-1392](1392-es256k-is-an-opt-in-package-that-registers-a-curve-and-an-ecdsa-algorithm-as-data.md)
- `src/NCode.Identity.Secrets/KeyTypes/EccSecretKeyTypeDescriptor.cs`,
  `src/NCode.Identity.Secrets.Abstractions/KeyTypes/EccCurveDomain.cs`
- Issue [#575](https://github.com/NCodeGroup/Identity/issues/575)
