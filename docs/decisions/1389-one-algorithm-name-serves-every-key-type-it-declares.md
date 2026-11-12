# 1389. One algorithm name serves every key type it declares

- **Status:** Accepted (amended by
  [ADR-1398](1398-a-key-size-selects-a-curve-only-when-no-other-curve-shares-it.md): a size selects a curve only when
  no other curve of the algorithm shares it, a key type refuses a curve requested with another size, a tenant key's
  type is the first that accepts its configuration, and only a symmetric key is checked by its type and size)
- **Date:** 2026-11-02
- **Deciders:** NCode Group
- **Amends:** [ADR-1067](1067-secret-key-types-form-one-catalog-and-authenticated-management-callers-discover-the-deployments-capabilities.md), the shape of the
  `algorithms` capability section, and [ADR-1169](1169-every-tenant-is-provisioned-with-persisted-keys.md),
  decision 2's rule that a key's type follows from its algorithm

## Context

A JOSE algorithm is registered once per name: the header's `alg` or `enc`, the `IAlgorithmCollection` lookup, the
settings that list allowed algorithms and the discovery metadata all key it by that name. Three consumers read the keys
an algorithm accepts: the JWE decryption key check, the tenant key provisioning of `DefaultTenantKeyPolicy`, and the
`algorithms` section of the management capabilities document.

[RFC 8037 §3.2](https://datatracker.ietf.org/doc/html/rfc8037#section-3.2) reuses the `ECDH-ES`, `ECDH-ES+A128KW`,
`ECDH-ES+A192KW` and `ECDH-ES+A256KW` names for key agreement over the `OKP` curves X25519 and X448. One name therefore
serves several `kty` values, `EC` and `OKP`, with different sizes for each. A key's type and size do not identify its
curve: P-256 and secp256k1 are both 256-bit `EC` curves, and Ed25519 and X25519 are both 256-bit `OKP` curves, of
which RFC 8037 §3.1 and §3.2 give one to signing and the other to key agreement. The `epk` header's members are defined
per `kty`, so the algorithm cannot write or read them for a key type it does not know, and a package that contributes a
key type, such as `NCode.Identity.Jose.EdDsa` with its `OKP` keys, must be able to bring its agreement
([#327](https://github.com/NCodeGroup/identity/issues/327)).

## Decision

1. **A keyed algorithm declares a list of key types.** `KeyedAlgorithm.KeyTypes` lists `AlgorithmKeyType` values, each
   a `SecretKey` CLR type (`KeyClrType`) with the sizes legal for keys of that type, in the algorithm's order of
   preference. A key is matched to the first entry its type derives from (`TryGetKeyType`), and its size is checked
   against that entry only. Most algorithms list one entry; the name is registered once. An entry for a key type on
   curves also lists the curves keys of its type must be on, each an `AlgorithmCurve` with its `crv` name and its key
   size, in the algorithm's order of preference, because two curves can share a size. `AlgorithmKeyType.OnCurves`
   builds such an entry and takes its sizes from its curves, so the sizes and the curves have one source.
2. **A key names its curve, and one rule checks it.** `AsymmetricSecretKey.CurveName` is the `crv` of the key's curve:
   an `OKP` key carries it, and an `ecc` key takes it from the elliptic-curve catalog's specification when the key is
   created, matched by the curve's OID, or has none when the catalog does not hold the curve.
   `AlgorithmKeyType.IsKeyCompatible` checks a key's type, size and, for an entry on curves, its curve name and size;
   `KeyedAlgorithm.IsKeyCompatible` applies it to the matched entry, and every ECDH agreement is chosen by it. An
   algorithm overrides `IsKeyCompatible` only for a check its key types cannot express, such as the ECDH-ES algorithms'
   need for exactly one agreement, and keeps its use-time checks, such as an `ECDSA` key's curve compared by OID when it
   signs.
3. **The ECDH-ES algorithms take their key types from registered agreements.** `IEcdhKeyAgreement`, in
   `NCode.Identity.Jose.Abstractions`, performs the Diffie-Hellman agreement over the curves of one key type, which its
   `AlgorithmKeyType` declares: it generates the ephemeral key pair on the recipient's curve and returns the shared
   secret `Z` and the ephemeral public key as a `SubjectPublicKeyInfo`, and on the recipient's side agrees from a
   `SecretKey`. The four ECDH-ES algorithms are a strategy collection over every registered agreement
   ([ADR-1001](1001-behavior-is-a-service-a-strategy-collection-or-a-mediator-seam-chosen-by-one-ladder.md),
   [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)), and the agreement is chosen per key, not per key
   type: a key is agreed by the one agreement whose `AlgorithmKeyType` accepts it, and a key that two agreements accept
   is refused as ambiguous. Several agreements may therefore serve one key type, each over its own curves, so a package
   can add ECDH-ES over another curve of the `EC` key type, such as a Brainpool curve, without replacing the core's
   agreement. The algorithms' `KeyTypes` merge the agreements that serve one key type into one entry with all their
   curves, and keep the order the agreements are registered in. The agreements of one key type all name their curves
   or all name sizes only, and a mix fails composition: an agreement without curves accepts every curve of its sizes,
   so the merged entry could list no curve, and a key would be generated and checked by its size alone. The order is
   the registration order: `AddJoseLibrary()` registers the core's `EC` agreement
   over P-256, P-384 and P-521 first, and a package registers its agreement beside its key type through the JOSE
   library's builder, so the host's order of packages is the order of preference. The Concat KDF stays in the
   algorithm and stays SHA-256 for every key type
   ([ADR-1126](1126-ecdh-es-concat-kdf-digest-is-sha-256-and-oracles-must-be-rfc-conforming.md)). In key agreement
   with key wrapping mode, the content encryption key is random and wrapped under the key agreed with the `epk`
   (RFC 7518 §4.6).
4. **The `epk` is a JSON Web Key of the recipient key's type, written and read through that type's descriptors.** The
   algorithm finds the key type's `ISecretKeyTypeDescriptor` and its `JsonWebKeyTypeDescriptor`
   ([ADR-1105](1105-secret-key-type-descriptor-first-class-seam.md),
   [ADR-1271](1271-a-key-type-registers-its-json-web-key-rules.md)); a key type without a JSON Web Key form cannot
   carry an ephemeral key and is refused.
   - Writing, the descriptor exports the ephemeral public key and only the type's required members (RFC 7638 §3.2) are
     kept, so the `epk` holds the minimum that represents the key and never a private member
     ([RFC 7518 §4.6.1.1](https://datatracker.ietf.org/doc/html/rfc7518#section-4.6.1.1)).
   - Reading, the `kty` must be the recipient key type's, the `epk` must hold no private member of that type nor of
     any standard type, and only the required members are imported, through the descriptor's validating public-key
     import. The agreement then refuses an ephemeral key on another curve than the recipient's and, for curves that
     have them, a degenerate shared secret such as the all-zero output of X25519 or X448 for a low-order point
     ([RFC 7748 §6](https://datatracker.ietf.org/doc/html/rfc7748#section-6)).
5. **A declared key is checked by the key, and a key to generate is resolved onto a curve.** A
   `KeyAlgorithmDeclaration` carries the key, or its public half, with the use and algorithm declared for it, and
   `ValidateKeyAlgorithm` checks it with the algorithm's `IsKeyCompatible`, the check the cryptographic runtime makes
   before it uses the key: an Ed25519 key declared for `ECDH-ES` or a P-256 key declared for `ES256K` is refused when it
   is registered, not only when it is first used. A key that is still to be generated is a `KeyGenerationDeclaration`
   (CLR type, optional size, optional curve), which `TryResolveKeyGeneration` resolves into a `ResolvedKeyGeneration`:
   for an entry on curves, the named curve, which the entry must list, or the first curve the entry lists with the
   requested size, or its first curve, and the curve's size; for an entry without curves, a declared curve is refused.
   Tenant key provisioning and management secret creation share that resolver, so no path checks a curve-bound
   algorithm by type and size alone. A held key of a type without curves whose material is not at hand, such as a
   stored symmetric key whose metadata the management API updates, is a `KeySizeDeclaration` (CLR type and size), which
   `ValidateKeySize` checks against the entry's sizes and refuses for an entry on curves, whose keys only the key itself
   can show to fit. The JWE decryption key check
   asks `IsKeyCompatible` too, so a decryption key on the wrong curve is reported as the caller's configuration error;
   the check reads only the configured key, never the token
   ([RFC 7516 §11.5](https://datatracker.ietf.org/doc/html/rfc7516#section-11.5)). A policy that needs a family of
   keys, such as the asymmetric keys a published key or a `private_key_jwt` assertion requires, asks
   `AcceptsOnly<TKey>()`, which holds when every declared key type derives from `TKey`.
6. **A tenant key may name its secret type and its curve.** `OpenIdTenantKeyOptions.SecretType`, when set, selects
   which of the algorithm's key types the key is generated as and must be one the algorithm accepts; unset, the key is
   the first declared key type that has a registered secret key type and can be provisioned, so a key type that cannot
   yields to the next. `OpenIdTenantKeyOptions.Curve`, when set, selects which of the entry's curves the key is
   generated on, so a contributed curve that shares a size with a NIST curve, such as a Brainpool curve for `ECDH-ES`,
   can be provisioned; unset, the resolver chooses it, so a 256-bit `OKP` key for `ECDH-ES` is an X25519 key and a
   448-bit one an X448 key. An unset size is the size of the chosen curve, or, for a key type without curves, the
   smallest the entry accepts. A persisted key satisfies the specification only when its secret type is the
   specification's and, for a key type on curves, its curve, read from its stored public half, is the specification's
   curve, whether that curve was configured or chosen by the resolver.
7. **The capabilities document lists each secret type with its own sizes and curves.**
   `AlgorithmCapabilityResource.KeyTypes` lists `{ secretType, keyBitSizes, curves }` per compatible secret type, each
   curve with its `curveName` and `curveSizeBits`, in the algorithm's order of preference. The management console
   offers the secret types the server advertises, bounds the key size by every size the chosen type's entry allows,
   and offers that entry's curves, taking the key size from the chosen curve; a secret type with curves takes a curve,
   and the first of them is chosen with the type.
8. **AEAD algorithms are not keyed algorithms.** An authenticated encryption algorithm's key is a content encryption
   key of `ContentKeySizeBits`, never a `SecretKey`, so `AuthenticatedEncryptionAlgorithm` derives from `Algorithm`.

## Options considered

- **One registration per (name, key type), resolved by both.** Rejected: every consumer of the collection resolves by
  name alone, from the header, settings and discovery metadata. Registering a name twice breaks the one-name lookup and
  pushes key-aware resolution to every caller, and the discovery metadata and settings would have to de-duplicate
  names that the specifications define once.
- **A separate algorithm class per key type behind a composite.** Rejected: the KDF, header handling and key wrap are
  identical across key types; only the agreement and the JSON Web Key form differ, and those already have seams (the
  agreement and the key type's descriptors).
- **Keep the `epk` members in the agreement.** Rejected: the key type's descriptors already own its JSON Web Key form,
  its required members and its private members; a second copy in each agreement could drift from them, and a
  contributed agreement could leak a private member the descriptor knows about.
- **Each algorithm and agreement checks a key's curve its own way.** Rejected: the curves an entry lists would be data
  that nothing reads, and a contributed algorithm that lists curves but checks only type and size would accept any
  curve. One rule over the key's `CurveName` keeps the declared curves and the check the same thing.
- **A declaration that names the key's type, size and, optionally, curve.** Rejected: a declaration without a curve
  could only be checked by type and size, which do not tell P-256 from secp256k1. A held key carries its curve, and a
  key to generate is resolved onto one; only a key of a type without curves is checked by its type and size.
- **Infer the tenant key's type from its size alone.** Rejected: `EC` P-256 and `OKP` X25519 keys are both 256 bits.
- **One agreement per key type, failing composition on an overlap.** Rejected: the core's `EC` agreement serves only
  the NIST curves, so no package could add ECDH-ES over another `EC` curve without replacing it, which closes the curve
  vocabulary that ADR-1150 keeps open. Choosing the agreement by the key keeps it open and still never lets two
  agreements derive the same key's content key.
- **A fixed order of agreements, or a priority on each.** Rejected: the order matters only for the key a tenant is
  provisioned with when nothing selects a type, the host's registration order already expresses which package it
  prefers, and `AddJoseLibrary()` registers the core's RFC 7518 agreement before any package can add one. A fixed rule
  would override the host, and a priority is a number every contributor would have to choose.

## Consequences

- A package adds ECDH-ES over its key type by registering an `IEcdhKeyAgreement` and its key type's descriptors; the
  algorithms, the decryption key check, tenant key provisioning and the capabilities document serve it unchanged.
- An algorithm or agreement declares its curves once, as data, and the declared curves are the check: a contributed
  algorithm that lists curves refuses a key on any other curve without code of its own.
- `ECDH-ES` refuses an `EC` key on a curve no registered agreement serves; the core serves P-256, P-384 and P-521.
- An `ecc` key on a curve the elliptic-curve catalog does not hold has no `CurveName`, so an algorithm bound to curves
  refuses it; registering the curve's `EccCurveSpecification` names it.
- A key whose curve the algorithm does not accept is refused when it is declared, imported or provisioned, not only
  when it is first used.

## References

- [RFC 7518 §4.6](https://datatracker.ietf.org/doc/html/rfc7518#section-4.6),
  [RFC 8037 §3.2](https://datatracker.ietf.org/doc/html/rfc8037#section-3.2),
  [RFC 7748 §6](https://datatracker.ietf.org/doc/html/rfc7748#section-6),
  [RFC 7638 §3.2](https://datatracker.ietf.org/doc/html/rfc7638#section-3.2)
- [ADR-1001](1001-behavior-is-a-service-a-strategy-collection-or-a-mediator-seam-chosen-by-one-ladder.md),
  [ADR-1067](1067-secret-key-types-form-one-catalog-and-authenticated-management-callers-discover-the-deployments-capabilities.md),
  [ADR-1105](1105-secret-key-type-descriptor-first-class-seam.md),
  [ADR-1126](1126-ecdh-es-concat-kdf-digest-is-sha-256-and-oracles-must-be-rfc-conforming.md),
  [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md),
  [ADR-1169](1169-every-tenant-is-provisioned-with-persisted-keys.md),
  [ADR-1271](1271-a-key-type-registers-its-json-web-key-rules.md)
- Issue [#327](https://github.com/NCodeGroup/identity/issues/327)
- `KeyedAlgorithm`, `AlgorithmKeyType`, `KeyAlgorithmDeclaration`, `KeyGenerationDeclaration` and `IEcdhKeyAgreement`
  in `NCode.Identity.Jose.Abstractions`; `AsymmetricSecretKey.CurveName` in `NCode.Identity.Secrets.Abstractions`;
  `EcdhKeyManagementAlgorithm` and `EccEcdhKeyAgreement` in `NCode.Identity.Jose`
