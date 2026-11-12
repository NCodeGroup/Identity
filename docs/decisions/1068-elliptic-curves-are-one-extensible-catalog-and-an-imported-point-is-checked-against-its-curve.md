# 1068. Elliptic curves are one extensible catalog, and an imported point is checked against its curve

- **Status:** Accepted
- **Date:** 2026-11-05
- **Deciders:** NCode Group

## Context

Elliptic-curve knowledge has to serve generation, validation, JWK publication and the management console, and a size
does not identify a curve: `secp256k1` and P-256 are both 256-bit. A registry keyed by size cannot generate a chosen
same-size curve and would mis-name the `crv` of a key on a non-NIST curve. Two registries, one for generation and one
for publication, would also drift from each other. A set of curves fixed in code is not extensible either, and the
family's vocabularies are open ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)).

The platform also judges an elliptic-curve point differently per operating system. Windows CNG answers an off-curve
secp256k1 point with `PlatformNotSupportedException`, and OpenSSL answers it with `CryptographicException`, so the same
invalid JWK public key fails with a different exception type depending on where the server runs. A peer key for
`ECDH-ES` must be on its curve as well, because an invalid-curve point is an attack on the key agreement. The curve
equation is arithmetic on public constants, so the check does not need the platform.

## Decision

Elliptic curves are one extensible catalog, curves are selected by identity, and a point is checked against its curve
before the platform imports it.

1. **One catalog.** `IEccCurveCatalog` (`NCode.Identity.Secrets.Abstractions`, namespace
   `NCode.Identity.Secrets.KeyTypes`) is the single source of curve knowledge for generation, validation, JWK
   publication and the console. `DefaultEccCurveCatalog` in `NCode.Identity.Secrets` seeds P-256, P-384 and P-521 and
   merges every registered `EccCurveSpecification` service, so one registered specification enables a curve
   everywhere.
2. **A specification carries its domain.** `EccCurveSpecification` holds `CurveName` (the `crv`), `CurveSizeBits`, the
   `ECCurve` and a required `Domain`. `EccCurveDomain` (`NCode.Identity.Secrets.KeyTypes`) holds the prime `p` and the
   coefficients `a` and `b` of the short-Weierstrass equation `y² ≡ x³ + ax + b (mod p)`. P-256, P-384, P-521 and
   secp256k1 use constants built with `EccCurveDomain.FromHex`, so their check is platform-independent. An application
   curve supplies constants or `EccCurveDomain.FromCurve(ECCurve)`, which reads the domain of another curve the
   platform supports, such as brainpoolP256r1.
3. **Curves are selected by identity.** `TryGetByName` and `TryGetByCurve` (the key's actual named-curve OID) name a
   key's `crv` unambiguously. A size (`GetByCurveSizeBits`) selects a curve only when no other curve shares it
   ([ADR-1398](1398-a-key-is-generated-on-one-named-curve-and-only-symmetric-keys-are-checked-by-size.md)).
4. **Generation takes the curve.** `SecretKeyGenerationParameters { KeySizeBits, Curve? }` carries the request. The
   ECC descriptor resolves the curve by name and refuses a size that differs from it. The `okp` and `AKP` descriptors
   take a curve too, an `AKP` key naming its parameter set there, and the symmetric and RSA descriptors refuse one
   ([ADR-1398](1398-a-key-is-generated-on-one-named-curve-and-only-symmetric-keys-are-checked-by-size.md)). `Curve`
   flows from `CreateSecretRequest` to `GenerateSecretRequest` to the descriptor.
5. **Publication reads the catalog.** The ECC descriptor's `ExportPublicKey`
   ([ADR-1104](1104-a-key-type-is-one-registered-descriptor-and-public-keys-are-imported-and-stored-by-encoding.md)) reads `crv` from the catalog by the key's actual
   curve. The capabilities document
   ([ADR-1067](1067-secret-key-types-form-one-catalog-and-authenticated-management-callers-discover-the-deployments-capabilities.md))
   has an `eccCurves` section (`EccCurveCapabilitiesProvider`), and the console's create-secret form offers a
   server-driven curve picker when `ecc` is selected.
6. **The descriptor checks the point first.** `EccSecretKeyTypeDescriptor.ParseJwkPublicKey` calls
   `EccCurveDomain.ContainsPoint(x, y)` before the platform imports. The point is valid when each coordinate is exactly
   the field's byte length (RFC 7518 §6.2.1.2–3), each is below `p`, and the point satisfies the curve equation. An
   invalid point throws `ArgumentException` on every platform, so a peer `ECDH-ES` key is on its curve before
   agreement.
7. **The platform still imports.** The platform creates the key from the checked parameters, and a failure that is the
   platform's own, such as a curve the platform does not support, keeps its exception.

## Options considered

- **Size-keyed registries in generation and in the JWKS layer.** Two curves can share a size, so the registry cannot
  generate a chosen same-size curve, mis-names a non-NIST key's `crv`, and the two registries drift apart.
- **Catching `PlatformNotSupportedException` beside `CryptographicException`.** It hides a real unsupported-curve
  error as an invalid point and leaves the check defined by the platform.
- **Reading the domain from `ExportExplicitParameters` at run time.** The platform does not offer it for every curve
  on every operating system, so the check would depend on the platform again; the constants do not.
- **One catalog selected by identity, with the domain in each specification (chosen).** Generation and JWK naming read
  the same data, and the point check is arithmetic on constants.

## Consequences

- Any curve is supported by registering one `EccCurveSpecification`; it is immediately available to generation,
  validation, JWK publication and the console picker.
- Same-size curves are disambiguated: a chosen curve is generated by name, and a key's `crv` is named from its actual
  curve.
- Generation and JWK naming cannot drift, because they read the same catalog.
- An off-curve point, a coordinate of the wrong length and a coordinate at or above `p` fail with `ArgumentException`
  identically on Windows and Linux.
- A curve registered by an application always supplies its domain, from constants or from `EccCurveDomain.FromCurve`.

## References

- [ADR-1067](1067-secret-key-types-form-one-catalog-and-authenticated-management-callers-discover-the-deployments-capabilities.md),
  [ADR-1104](1104-a-key-type-is-one-registered-descriptor-and-public-keys-are-imported-and-stored-by-encoding.md),
  [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md),
  [ADR-1392](1392-ecdsa-and-eddsa-algorithms-are-fully-specified-and-bound-to-their-curve-and-es256k-is-opt-in.md),
  [ADR-1398](1398-a-key-is-generated-on-one-named-curve-and-only-symmetric-keys-are-checked-by-size.md)
- `src/NCode.Identity.Secrets/KeyTypes/EccSecretKeyTypeDescriptor.cs`,
  `src/NCode.Identity.Secrets.Abstractions/KeyTypes/EccCurveDomain.cs`
