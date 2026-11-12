# 1398. A key is generated on one named curve, and only symmetric keys are checked by size

- **Status:** Accepted
- **Date:** 2026-11-03
- **Deciders:** NCode Group

## Context

A key to generate is described by a key type, a size and, optionally, a curve: the `JWK` `crv` name that the `ecc`,
`okp` and `AKP` key types take (an `AKP` key names its ML-DSA parameter set in the same place). Three paths turn such a
description into a key: management secret creation, tenant key provisioning and any caller of
`ISecretKeyTypeDescriptor.GenerateKeyMaterial`. The first two resolve it against an algorithm, then check it with the
key type's `ValidateGeneration`; the third reaches the key type alone, as does a management request that names no
algorithm.

A curve fixes the size of its keys: P-256 keys are 256 bits, Ed448 keys 456 bits, ML-DSA-65 keys 15616 bits. The key
type's descriptor holds that fact for its own curves, including those an application or a package registers, and an
algorithm's `AlgorithmCurve` list restates it so that a size can select one of the algorithm's curves and the
capabilities document can show each curve's size.

Two curves of one algorithm, or of one key type's catalog, can share a size. The core's `ECDH-ES` agreement serves
P-256, and a package that adds an agreement over brainpoolP256r1 gives `ECDH-ES` a second 256-bit `EC` curve;
`AddSecp256k1()` registers secp256k1 beside P-256 in the elliptic-curve catalog, so the `ecc` key type holds two
256-bit curves. The two are different keys with different provenance, and the size, which states a key's strength, does
not say which of them is meant. The `okp` key type's curves differ by use as well as by size: Ed25519 signs and X25519
agrees keys, and both are 256 bits, while the descriptor has no algorithm to tell a signing request from an agreement
request. The `AKP` parameter sets, ML-DSA-44, ML-DSA-65 and ML-DSA-87, have distinct sizes.

A held key whose material is not at hand is checked by `ValidateKeySize` over its type and size. A symmetric key is a
byte string: its type and size are all of its parameters, and its material is protected under the key hierarchy
([ADR-1235](1235-stored-secrets-and-grant-payloads-are-protected-by-a-key-hierarchy-under-an-external-root-key.md)). An
asymmetric key has parameters that its type and size do not show, such as its curve, its parameter set or the
parameters of a key type a package contributes, which an algorithm's `IsKeyCompatible` may check, and every stored
asymmetric secret holds its public half
([ADR-1268](1268-an-asymmetric-secrets-public-key-is-stored-beside-it-and-read-without-its-private-key.md)).

## Decision

1. **A key to generate is a declaration that resolves onto one curve.** A `KeyGenerationDeclaration` holds a CLR type,
   an optional size and an optional curve. `TryResolveKeyGeneration` resolves it into a `ResolvedKeyGeneration`: a
   declared curve must be one of the entry's curves, a declared size passes through unchanged, the curve's size is
   taken only when no size is declared, and an entry without curves refuses a declared curve.
2. **A size selects a curve only when exactly one curve has it.** A declaration with a size and no curve resolves onto
   the one curve of that size that the algorithm's key type lists (`AlgorithmKeyType.GetCurves`); a size that several
   curves share is refused with an error that names them. A declaration with neither size nor curve takes the
   algorithm's first curve, its stated preference. The curve is named by `KeyGenerationDeclaration.Curve`,
   `OpenIdTenantKeyOptions.Curve` or `CreateSecretRequest.Curve`; an unset size is the chosen curve's size, or the
   smallest legal size for a key type without curves.
3. **A key type refuses a curve requested with another size.** The `ecc`, `okp` and `AKP` descriptors refuse, in
   `ValidateGeneration` and in `GenerateKeyMaterial`, a curve or parameter set whose size is not
   `SecretKeyGenerationParameters.KeySizeBits`, so the rule holds on every path, callers without an algorithm included.
   An `okp` key is generated only on a named curve, because Ed25519 and X25519 share a size and the descriptor has no
   algorithm to tell their uses apart.
4. **The `ecc` key type applies the same rule to its catalog.** An `ecc` key requested without a curve is generated on
   the catalog curve of that size (`IEccCurveCatalog.GetByCurveSizeBits`) only when exactly one catalog curve has it,
   and is otherwise refused with the curves named, so a host with `AddSecp256k1()` names the curve of every 256-bit
   `ecc` key it requests without an algorithm. A JWK names an `ecc` curve by its OID alone, so a key on a curve the
   catalog does not hold is not published.
5. **Provisioning and creation share the resolver.** Tenant provisioning (`DefaultTenantKeyPolicy`) and management
   secret creation (`DefaultSecretValidator`) resolve through it and pass the resolved curve on.
   `ISecretValidator.ValidateCreate` returns a `SecretCreateValidation`, and the `GenerateSecretRequest` carries its
   `GenerationParameters` whole, so an endpoint generates exactly what was validated.
6. **A tenant key's type is the first that accepts its configuration, and that type decides.** When
   `OpenIdTenantKeyOptions.SecretType` is set, it selects the key type, which must be one the algorithm accepts. When it
   is unset, the key is the first key type with a registered asymmetric secret type that accepts the configured curve,
   or, without a curve, the configured size. That key type's resolution is the outcome: a configuration it refuses, such
   as a size two of its curves share, is reported and never provisioned as a later key type. When none accepts, the
   error lists the curves and sizes of every provisionable key type of the algorithm.
7. **A persisted key satisfies a tenant key specification only when it fits.** Its secret type matches, its stored
   public half is on the specification's curve and `IsKeyCompatible` accepts it. A missing or unreadable public half
   does not satisfy it, so a key is provisioned
   ([ADR-1268](1268-an-asymmetric-secrets-public-key-is-stored-beside-it-and-read-without-its-private-key.md)).
8. **Only a symmetric key is checked by its type and size.** `ValidateKeySize` checks a `KeySizeDeclaration` (CLR type,
   size) only for a symmetric key, and refuses an asymmetric type (one deriving from `AsymmetricSecretKey`, as
   `ISecretKeyTypeDescriptor.IsAsymmetric` reports) and an entry bound to curves. An asymmetric key is checked by the key
   itself, or its stored public half, through `ValidateKeyAlgorithm` and the algorithm's `IsKeyCompatible`. A
   contributed key type whose keys have parameters beyond their type and size derives from `AsymmetricSecretKey`; one
   whose keys do not is a byte string that its type and size describe.

The outcomes: `ES256K` provisions a secp256k1 key, `Ed25519` an Ed25519 key, `ECDH-ES` with `okp` gives X25519 at 256
bits and X448 at 448, and the polymorphic `EdDSA` gives Ed25519 at 256 and Ed448 at 456.

## Options considered

- **Check the size against a named curve in the resolver as well as in the key type.** Rejected: the descriptor is
  where a curve's size is known, for every curve its catalog holds, and it is the one check that a caller of
  `GenerateKeyMaterial` without an algorithm also meets. A second check in the resolver restates the descriptor's data
  and cannot cover the paths that have no algorithm.
- **Let a size take the first of the algorithm's curves, or the catalog's built-in curve, that has it.** Rejected: the
  size states the key's strength, not its curve, so the first curve of the size is a guess that the configuration did
  not make. A Brainpool key and a P-256 key are not interchangeable to the parties that receive them, and refusing the
  size asks for the one fact that tells them apart. A request that names no algorithm reaches the key type alone, so the
  key type refuses the shared size itself, or the management API would generate exactly the key that provisioning
  refuses.
- **Let the `okp` key type select a curve by a size only one of its curves has.** Rejected: an `okp` curve is chosen by
  its use as much as by its size, which the descriptor cannot know without an algorithm, and a rule that selects X448
  at 448 bits but refuses 256 bits would make acceptance depend on which curves happen to share a size.
- **Yield to the algorithm's next key type when the first refuses the configuration, or report through the first
  one.** Rejected: an `EC` configuration that is ambiguous or wrong would provision an `OKP` key the configuration did
  not describe, and the first key type's error names only its own curves, so an operator who misspells an `OKP` curve
  is told the `EC` curves alone. A key type is passed over only when it cannot take the configuration at all.
- **Check every held key whose material is not at hand by its type and size.** Rejected: the type and size of an
  asymmetric key do not show its curve or parameter set, so the check would accept a key that the algorithm refuses
  when it is first used, and the stored public half that shows them is always at hand.
- **Unprotect a symmetric key to check it by the key itself.** Rejected: its type and size already describe it whole,
  and a metadata update has no reason to unprotect secret material.
- **State, as a separate flag, whether type and size describe a key whole.** Rejected: the key's runtime type already
  states it by deriving from `AsymmetricSecretKey`, and a second fact could contradict the first.
- **A key is generated on one named curve, and only a symmetric key is checked by size.** Chosen: the descriptor owns
  each curve's size, a shared size is refused, and an asymmetric key is checked by the key or its public half.

## Consequences

- A request for P-256 at 384 bits, Ed25519 at 456 bits or ML-DSA-65 at the size of ML-DSA-44 is refused everywhere:
  by management creation, tenant provisioning and the key types alike.
- A host with `AddSecp256k1()` names the curve of every 256-bit `ecc` key it requests without an algorithm; a
  configuration that sets neither size nor curve keeps the algorithm's first curve. The management console always
  names a curve, so its requests are never ambiguous.
- A contributed descriptor checks its own curves' sizes in `ValidateGeneration`, or a size requested with one of its
  curves is not checked.
- An asymmetric key is never declared for an algorithm on its type and size alone.
- An error for a tenant key that no key type of its algorithm accepts names the curves and sizes of every key type a
  tenant can be provisioned with.

## References

- Issue [#534](https://github.com/NCodeGroup/identity/issues/534)
- [ADR-1235](1235-stored-secrets-and-grant-payloads-are-protected-by-a-key-hierarchy-under-an-external-root-key.md),
  [ADR-1268](1268-an-asymmetric-secrets-public-key-is-stored-beside-it-and-read-without-its-private-key.md),
  [ADR-1389](1389-one-algorithm-name-serves-every-key-type-and-curve-it-declares-and-ecdh-es-agreements-are-registered.md),
  [ADR-1392](1392-ecdsa-and-eddsa-algorithms-are-fully-specified-and-bound-to-their-curve-and-es256k-is-opt-in.md)
- `AlgorithmKeyType.GetCurves`, `AlgorithmCollectionExtensions.TryResolveKeyGeneration` and `ValidateKeySize` in
  `NCode.Identity.Jose.Abstractions`; `IEccCurveCatalog.GetByCurveSizeBits` in `NCode.Identity.Secrets.Abstractions`;
  `EccSecretKeyTypeDescriptor` and `AkpSecretKeyTypeDescriptor` in `NCode.Identity.Secrets`;
  `OkpSecretKeyTypeDescriptor` in `NCode.Identity.Jose.EdDsa`; `DefaultTenantKeyPolicy` in
  `NCode.Identity.OpenId.Core`; `DefaultSecretValidator` in `NCode.Identity.OpenId.Management`
