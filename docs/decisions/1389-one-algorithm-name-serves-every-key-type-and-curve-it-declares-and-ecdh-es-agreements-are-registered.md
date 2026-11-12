# 1389. One algorithm name serves every key type and curve it declares, and ECDH-ES agreements are registered

- **Status:** Accepted
- **Date:** 2026-11-02
- **Deciders:** NCode Group

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

One registered name serves every key type and curve its algorithm declares, and the ECDH-ES algorithms take their key
agreements from a registered collection. The rule is stated plainly: a key is accepted when it matches a declared key
type, in size and, for a key type on curves, in curve, and an ECDH-ES key is agreed by the one registered agreement that
accepts it.

1. **A keyed algorithm declares a list of key types.** `KeyedAlgorithm.KeyTypes` lists `AlgorithmKeyType` values, each
   a `SecretKey` CLR type (`KeyClrType`) with the `KeyBitSizes` legal for keys of that type, in the algorithm's order of
   preference. A key is matched to the first entry its type derives from (`TryGetKeyType`), and its size is checked
   against that entry only. Most algorithms list one entry; the name is registered once in `IAlgorithmCollection`.
2. **An entry on curves lists them.** An entry for a key type on curves lists `AlgorithmCurve` values, each with a
   `crv` name and a key size, in the algorithm's order of preference, because two curves can share a size.
   `AlgorithmKeyType.OnCurves` builds such an entry and derives its sizes from its curves, so the sizes and the curves
   have one source.
3. **A key names its curve, and one rule checks it.** `AsymmetricSecretKey.CurveName` is the `crv` of the key's curve:
   an `OKP` key carries it, and an `ecc` key takes it from the elliptic-curve catalog, matched by the curve's OID, when
   the key is created, or has none when the catalog does not hold the curve. `AlgorithmKeyType.IsKeyCompatible` checks
   a key's type, size and curve; `KeyedAlgorithm.IsKeyCompatible` applies it to the matched entry. An algorithm
   overrides `IsKeyCompatible` only for a check its key types cannot express, and keeps its use-time checks, such as an
   `ECDSA` key's curve compared by OID when it signs.
4. **The ECDH-ES algorithms take their key types from registered agreements.** `IEcdhKeyAgreement`, in
   `NCode.Identity.Jose.Abstractions`, performs the Diffie-Hellman agreement over the curves of one key type: it
   generates the ephemeral key pair on the recipient's curve and returns the shared secret `Z` and the ephemeral public
   key as a `SubjectPublicKeyInfo`. The four ECDH-ES algorithms are a strategy collection over every registered
   agreement
   ([ADR-1001](1001-behavior-is-a-service-a-strategy-collection-or-a-mediator-seam-chosen-by-one-ladder.md),
   [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)).
5. **The agreement is chosen per key.** Exactly one agreement must accept the key, and a key that two agreements accept
   is refused as ambiguous. Several agreements may serve one key type, each over its own curves, so a package can add
   ECDH-ES over another curve of the `EC` key type, such as a Brainpool curve, without replacing the core's agreement.
   The algorithms' `KeyTypes` merge the agreements of one key type into one entry in registration order. The agreements
   of one key type all name their curves or all name sizes only, and a mix fails composition: an agreement without
   curves accepts every curve of its sizes, so the merged entry could list no curve.
6. **Registration order is the preference order.** `AddJoseLibrary()` registers `EccEcdhKeyAgreement` over P-256,
   P-384 and P-521 first, and a package registers its agreement beside its key type through the JOSE library's builder,
   so the host's order of packages is the order of preference.
7. **The KDF and the content key.** The Concat KDF lives in the algorithm and is SHA-256 for every key type
   ([ADR-1126](1126-the-ecdh-es-concat-kdf-digest-is-sha-256-and-a-test-oracle-must-conform-to-the-rfc.md)). In key
   agreement with key wrapping, the content encryption key is random and wrapped under the key agreed with the `epk`
   (RFC 7518 §4.6).
8. **The `epk` is a JSON Web Key of the recipient key's type, written and read through that type's descriptors.** The
   algorithm finds the key type's `ISecretKeyTypeDescriptor` and its `JsonWebKeyTypeDescriptor`
   ([ADR-1104](1104-a-key-type-is-one-registered-descriptor-and-public-keys-are-imported-and-stored-by-encoding.md),
   [ADR-1119](1119-a-di-free-jwk-package-models-keys-sets-and-thumbprints-and-key-types-register-their-rules.md)); a key
   type with no JSON Web Key form cannot carry an ephemeral key and is refused.
   - Writing keeps only the type's required members (RFC 7638 §3.2), so the `epk` holds the minimum that represents the
     key and never a private member
     ([RFC 7518 §4.6.1.1](https://datatracker.ietf.org/doc/html/rfc7518#section-4.6.1.1)).
   - Reading requires the recipient key type's `kty` and no private member of that type or of any standard type, and
     imports only the required members through the descriptor's validating public-key import.
9. **The agreement refuses unsafe ephemeral keys.** It refuses an `epk` on another curve than the recipient's and a
   degenerate all-zero shared secret
   ([RFC 7748 §6](https://datatracker.ietf.org/doc/html/rfc7748#section-6)).
10. **A declared key is checked by the key.** `ValidateKeyAlgorithm` checks a `KeyAlgorithmDeclaration` with
    `IsKeyCompatible` when the key is registered, the check the cryptographic runtime makes before it uses the key: an
    Ed25519 key declared for `ECDH-ES` or a P-256 key declared for `ES256K` is refused at registration, not when it is
    first used. Key generation and size declarations are decided in
    [ADR-1398](1398-a-key-size-selects-a-curve-only-when-no-other-curve-shares-it.md).
11. **The decryption key check reads only the configured key.** The JWE decryption-key check asks `IsKeyCompatible`
    too, so a decryption key on the wrong curve is reported as the caller's configuration error, and it never reads
    the token ([RFC 7516 §11.5](https://datatracker.ietf.org/doc/html/rfc7516#section-11.5)). A policy that needs a
    family of keys, such as the asymmetric keys a published key or a `private_key_jwt` assertion requires, asks
    `AcceptsOnly<TKey>()`.
12. **The capabilities document lists each secret type with its own sizes and curves.**
    `AlgorithmCapabilityResource.KeyTypes` lists an `AlgorithmKeyTypeCapabilityResource { SecretType, KeyBitSizes,
    Curves }` per secret type, in preference order, each curve an `AlgorithmCurveCapabilityResource { CurveName,
    CurveSizeBits }`. The management console offers the advertised secret types and curves and takes the key size from
    the chosen curve.
13. **AEAD algorithms are not keyed.** `AuthenticatedEncryptionAlgorithm` derives from `Algorithm`, and its key is a
    content encryption key of `ContentKeySizeBits`, never a `SecretKey`.

## Options considered

- **One registration per (name, key type), resolved by both.** Rejected: every consumer of the collection resolves by
  name alone, from the header, settings and discovery metadata. Registering a name twice breaks the one-name lookup and
  pushes key-aware resolution to every caller.
- **A separate algorithm class per key type behind a composite.** Rejected: the KDF, header handling and key wrap are
  identical across key types; only the agreement and the JSON Web Key form differ, and those already have seams.
- **Keep the `epk` members in the agreement.** Rejected: the key type's descriptors already own its JSON Web Key form,
  its required members and its private members; a second copy in each agreement could drift from them, and a
  contributed agreement could leak a private member the descriptor knows about.
- **Each algorithm checks a key's curve its own way.** Rejected: the curves an entry lists would be data that nothing
  reads, and a contributed algorithm that lists curves but checks only type and size would accept any curve. One rule
  over the key's `CurveName` keeps the declared curves and the check the same thing.
- **A declaration of type and size only.** Rejected: it cannot tell P-256 from secp256k1.
- **Infer a key's type from its size.** Rejected: `EC` P-256 and `OKP` X25519 keys are both 256 bits.
- **One agreement per key type, failing composition on an overlap.** Rejected: the core's `EC` agreement serves only
  the NIST curves, so no package could add ECDH-ES over another `EC` curve without replacing it, which closes the curve
  vocabulary that ADR-1150 keeps open.
- **A fixed order or a priority on agreements.** Rejected: the host's registration order already expresses which
  package it prefers, and `AddJoseLibrary()` registers the core's RFC 7518 agreement before any package can add one. A
  fixed rule would override the host, and a priority is a number every contributor would have to choose.
- **Chosen:** choosing the agreement by the key keeps the vocabulary open and never lets two agreements derive the same
  key's content key.

## Consequences

- A package adds ECDH-ES over its key type by registering an `IEcdhKeyAgreement` and its key type's descriptors; the
  algorithms, the decryption key check, tenant key provisioning and the capabilities document serve it unchanged.
- An algorithm or agreement declares its curves once, as data, and the declared curves are the check: a contributed
  algorithm that lists curves refuses a key on any other curve without code of its own.
- `ECDH-ES` refuses an `EC` key on a curve no registered agreement serves; the core serves P-256, P-384 and P-521.
- An `ecc` key on a curve the elliptic-curve catalog does not hold has no `CurveName`, so every curve-bound algorithm
  refuses it; registering the curve's `EccCurveSpecification` names it.
- A key whose curve the algorithm does not accept is refused when it is declared, imported or provisioned, not only
  when it is first used.

## References

- [RFC 7518 §4.6](https://datatracker.ietf.org/doc/html/rfc7518#section-4.6),
  [RFC 8037 §3.2](https://datatracker.ietf.org/doc/html/rfc8037#section-3.2),
  [RFC 7748 §6](https://datatracker.ietf.org/doc/html/rfc7748#section-6),
  [RFC 7638 §3.2](https://datatracker.ietf.org/doc/html/rfc7638#section-3.2)
- [ADR-1001](1001-behavior-is-a-service-a-strategy-collection-or-a-mediator-seam-chosen-by-one-ladder.md),
  [ADR-1104](1104-a-key-type-is-one-registered-descriptor-and-public-keys-are-imported-and-stored-by-encoding.md),
  [ADR-1119](1119-a-di-free-jwk-package-models-keys-sets-and-thumbprints-and-key-types-register-their-rules.md),
  [ADR-1126](1126-the-ecdh-es-concat-kdf-digest-is-sha-256-and-a-test-oracle-must-conform-to-the-rfc.md),
  [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md),
  [ADR-1398](1398-a-key-size-selects-a-curve-only-when-no-other-curve-shares-it.md)
- Issue [#327](https://github.com/NCodeGroup/identity/issues/327)
- `KeyedAlgorithm`, `AlgorithmKeyType`, `AlgorithmCurve`, `KeyAlgorithmDeclaration` and `IEcdhKeyAgreement` in
  `NCode.Identity.Jose.Abstractions`; `AsymmetricSecretKey.CurveName` in `NCode.Identity.Secrets.Abstractions`;
  `EcdhKeyManagementAlgorithm` and `EccEcdhKeyAgreement` in `NCode.Identity.Jose`
