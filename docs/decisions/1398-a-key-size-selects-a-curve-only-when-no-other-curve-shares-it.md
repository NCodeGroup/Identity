# 1398. A key size selects a curve only when no other curve shares it, and only a symmetric key is checked by its type and size

- **Status:** Accepted
- **Date:** 2026-11-03
- **Deciders:** NCode Group
- **Amends:** [ADR-1389](1389-one-algorithm-name-serves-every-key-type-it-declares.md), decision 5's resolution of a
  key to generate and its check of a held key by type and size, and decision 6's choice of a tenant key's type;
  [ADR-1392](1392-es256k-is-an-opt-in-package-that-registers-a-curve-and-an-ecdsa-algorithm-as-data.md), decision 5's
  choice of P-256 as the curve the `ecc` key type selects by size

## Context

A key to generate is described by a key type, a size and, optionally, a curve, the `JWK` `crv` name that the `ecc`,
`okp` and `AKP` key types take (an `AKP` key names its ML-DSA parameter set in the same place). Three paths turn such
a description into a key: management secret creation, tenant key provisioning and any caller of
`ISecretKeyTypeDescriptor.GenerateKeyMaterial`. The first two resolve it against an algorithm with
`TryResolveKeyGeneration`, then check it with the key type's `ValidateGeneration`; the third reaches the key type
alone, as does a management request that names no algorithm.

A curve fixes the size of its keys: P-256 keys are 256 bits, Ed448 keys 456 bits, ML-DSA-65 keys 15616 bits. The key
type's descriptor holds that fact for its own curves, including those an application or a package registers, and an
algorithm's `AlgorithmCurve` list restates it so that a size can select one of the algorithm's curves and the
capabilities document can show each curve's size.

Two curves of one algorithm, or of one key type's catalog, can share a size. The core's `ECDH-ES` agreement serves
P-256, and a package that adds an agreement over brainpoolP256r1 gives `ECDH-ES` a second 256-bit `EC` curve;
`AddSecp256k1()` registers secp256k1 beside P-256 in the elliptic-curve catalog, so the `ecc` key type holds two
256-bit curves. The two are different keys with different provenance, and the size, which states a key's strength,
does not say which of them is meant. The `okp` key type's curves differ by use as well as by size: Ed25519 signs and
X25519 agrees keys, and both are 256 bits, while the descriptor has no algorithm to tell a signing request from an
agreement request. The `AKP` parameter sets, ML-DSA-44, ML-DSA-65 and ML-DSA-87, have distinct sizes.

A held key whose material is not at hand is checked by `ValidateKeySize` over its type and size. A symmetric key is a
byte string: its type and size are all of its parameters, and its material is protected under the key hierarchy
([ADR-1235](1235-stored-secrets-and-grant-payloads-are-protected-by-a-key-hierarchy-under-an-external-root-key.md)). An asymmetric key has
parameters that its type and size do not show, such as its curve, its parameter set or the parameters of a key type a
package contributes, which an algorithm's `IsKeyCompatible` may check, and every stored asymmetric secret holds its
public half ([ADR-1268](1268-a-generated-private-key-stores-its-public-key-beside-it.md)).

## Decision

1. **A key type refuses a curve requested with another size.** The `ecc`, `okp` and `AKP` descriptors refuse, in
   `ValidateGeneration` and in `GenerateKeyMaterial`, a named curve or parameter set whose keys are not the requested
   `SecretKeyGenerationParameters.KeySizeBits`. An `okp` key is generated only on a named curve, because a signing
   curve can share a size with an agreement curve, as Ed25519 and X25519 do, and the descriptor has no algorithm to
   tell them apart. Every path that generates a key reaches the descriptor, so the rule has one home.
   `TryResolveKeyGeneration` checks that a declared curve is one of the algorithm's curves and passes the declared
   size on unchanged, taking the curve's size only when none is declared; its `ResolvedKeyGeneration` is checked by
   the descriptor before a key is generated from it.
2. **A size selects a curve only when exactly one curve has it, in the algorithm and in the key type alike.** For a
   key type on curves, `TryResolveKeyGeneration` resolves a declaration with a size and no curve onto the one curve of
   that size that the algorithm's key type lists (`AlgorithmKeyType.GetCurves`), and refuses a size that several of
   its curves have, with an error that names those curves. The curve is then named: `KeyGenerationDeclaration.Curve`,
   `OpenIdTenantKeyOptions.Curve` or `CreateSecretRequest.Curve`. A declaration with neither a size nor a curve takes the
   algorithm's first curve, its stated preference. The `ecc` key type applies the same rule to its catalog: a request
   without a curve, which reaches the key type alone when it names no algorithm, is generated on the one catalog curve
   of the requested size (`IEccCurveCatalog.GetByCurveSizeBits`), and refused with the curves named when several have
   it, so a host with `AddSecp256k1()` names the curve of every 256-bit `ecc` key it requests without an algorithm. A
   JSON Web Key names an `ecc` key's curve by its OID alone, so a key on a curve the catalog does not hold is not
   published rather than published under the name of a curve of its size.
3. **A tenant key's type is the first that accepts its configuration, and that type decides.** When
   `OpenIdTenantKeyOptions.SecretType` is unset, the key is generated as the first of the algorithm's key types that has
   a registered asymmetric secret key type and accepts the configured curve, which must be one of its curves, or,
   without a curve, the configured size, which must be one of its sizes. That key type's resolution is the outcome: a
   configuration it refuses, such as a size that two of its curves share or a curve with another size, is reported and
   never provisioned as a later key type. When no key type accepts the configuration, the error lists what every
   provisionable key type of the algorithm accepts, the curves of a key type on curves and the sizes of one without, so
   an `ECDH-ES` key configured on a curve the algorithm does not accept is told of its `EC` and its `OKP` curves alike.
4. **Only a symmetric key is checked by its type and size.** `ValidateKeySize` refuses an asymmetric key type, as it
   refuses a key type the algorithm binds to curves, so an asymmetric key is always checked by the key itself, or its
   stored public half, with `ValidateKeyAlgorithm` and the algorithm's `IsKeyCompatible`. A `KeySizeDeclaration`
   describes a held symmetric key, whose type and size are the whole of what an algorithm can accept it on, and whose
   material a metadata check never unprotects. A key type states that its keys are asymmetric by deriving from
   `AsymmetricSecretKey`, the base that holds a key's curve name and public half and the fact
   `ISecretKeyTypeDescriptor.IsAsymmetric` reports, so `ValidateKeySize` asks the declared runtime type, as
   `ValidateKeyAlgorithm` asks a key for its curve; a contributed key type whose keys have parameters beyond their
   type and size derives from it, and one whose keys do not is a byte string that its type and size describe.

## Options considered

- **Check the size against a named curve in the resolver as well as in the key type.** Rejected: the descriptor is
  where a curve's size is known, for every curve its catalog holds, and it is the one check that a caller of
  `GenerateKeyMaterial` without an algorithm also meets. A second check in the resolver restates the descriptor's
  data and cannot cover the paths that have no algorithm.
- **Let a size take the first of the algorithm's curves, or the catalog's built-in curve, that has it.** Rejected: the
  size states the key's strength, not its curve, so the first curve of the size is a guess that the configuration did
  not make, whether the algorithm's list or the key type's catalog makes it. A Brainpool key and a P-256 key are not
  interchangeable to the parties that receive them, and refusing the size asks for the one fact that tells them apart.
  A request that names no algorithm reaches the key type alone, so the key type refuses the shared size itself, or the
  management API would generate exactly the key that provisioning refuses.
- **Let the `okp` key type select a curve by a size only one of its curves has.** Rejected: an `okp` curve is chosen
  by its use as much as by its size, which the descriptor cannot know without an algorithm, and a rule that selects
  X448 at 448 bits but refuses 256 bits would make the request's acceptance depend on which curves happen to share a
  size. Every `okp` request names its curve, and the algorithm's resolver names it for provisioning and creation.
- **Yield to the algorithm's next key type when the first refuses the configuration.** Rejected: an `EC`
  configuration that is ambiguous or wrong would provision an `OKP` key, a different key that the configuration did
  not describe. A key type is passed over only when it cannot take the configuration at all.
- **Report a refused configuration through the algorithm's first key type.** Rejected: that key type's error names
  only its own curves and sizes, so an operator who configures an `ECDH-ES` key on a misspelled `OKP` curve is told
  the `EC` curves alone, as if the `OKP` curves were not accepted.
- **Check every held key whose material is not at hand by its type and size.** Rejected: the type and size of an
  asymmetric key do not show its curve or parameter set, so the check would accept a key that the algorithm refuses
  when it is first used, and the stored public half that shows them is always at hand.
- **Unprotect a symmetric key to check it by the key itself.** Rejected: its type and size already describe it whole,
  and a metadata update has no reason to unprotect secret material.
- **State on the `AlgorithmKeyType` or the key type's descriptor, as a separate fact, whether type and size describe
  a key whole.** Rejected: the key's runtime type already states it by deriving from `AsymmetricSecretKey`, which is
  what `ISecretKeyTypeDescriptor.IsAsymmetric` reports and what a key's curve name and public half hang on, and a
  second fact could contradict the first; an algorithm's key type names the runtime type it accepts, so `ValidateKeySize`
  reads the fact there.

## Consequences

- A request for P-256 at 384 bits, Ed25519 at 456 bits or ML-DSA-65 at the size of ML-DSA-44 is refused, by
  management creation, tenant provisioning and the key types alike.
- A host that adds a curve that shares a size with one of an algorithm's curves, or with one of the catalog's, names
  the curve of every key it sizes by that size; a configuration that sets neither size nor curve keeps the algorithm's
  first curve. The management console names a curve for every secret type with curves, so its requests are never
  ambiguous.
- A contributed key type's descriptor checks its own curves' sizes in `ValidateGeneration`, or a size requested with
  one of its curves is not checked.
- An asymmetric key is never declared for an algorithm on its type and size alone.
- An error for a tenant key that no key type of its algorithm accepts names the curves and sizes of every key type a
  tenant can be provisioned with.

## References

- Issue [#534](https://github.com/NCodeGroup/identity/issues/534)
- [ADR-1235](1235-stored-secrets-and-grant-payloads-are-protected-by-a-key-hierarchy-under-an-external-root-key.md),
  [ADR-1268](1268-a-generated-private-key-stores-its-public-key-beside-it.md),
  [ADR-1389](1389-one-algorithm-name-serves-every-key-type-it-declares.md),
  [ADR-1392](1392-es256k-is-an-opt-in-package-that-registers-a-curve-and-an-ecdsa-algorithm-as-data.md)
- `AlgorithmKeyType.GetCurves` and `AlgorithmCollectionExtensions.TryResolveKeyGeneration` and `ValidateKeySize` in
  `NCode.Identity.Jose.Abstractions`; `IEccCurveCatalog.GetByCurveSizeBits` in `NCode.Identity.Secrets.Abstractions`;
  `EccSecretKeyTypeDescriptor` and `AkpSecretKeyTypeDescriptor` in `NCode.Identity.Secrets`;
  `OkpSecretKeyTypeDescriptor` in `NCode.Identity.Jose.EdDsa`; `DefaultTenantKeyPolicy` in
  `NCode.Identity.OpenId.Core`; `DefaultSecretValidator` in `NCode.Identity.OpenId.Management`
