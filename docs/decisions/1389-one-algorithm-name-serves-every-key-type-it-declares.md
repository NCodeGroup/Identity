# 1389. One algorithm name serves every key type it declares

- **Status:** Accepted
- **Date:** 2026-11-02
- **Deciders:** NCode Group
- **Amends:** [ADR-1067](1067-secret-type-capability-catalog-and-capabilities-discovery.md), the shape of the
  `algorithms` capability section, and [ADR-1169](1169-every-tenant-is-provisioned-with-persisted-keys.md),
  decision 2's rule that a key's type follows from its algorithm

## Context

A JOSE algorithm is registered once per name: the header's `alg` or `enc`, the `IAlgorithmCollection` lookup, the
settings that list allowed algorithms and the discovery metadata all key it by that name. Each keyed algorithm declared
one CLR key type and one set of legal key sizes, and three consumers read them: the JWE decryption key check, the tenant
key provisioning of `DefaultTenantKeyPolicy`, and the `algorithms` section of the management capabilities document.

[RFC 8037 §3.2](https://datatracker.ietf.org/doc/html/rfc8037#section-3.2) reuses the `ECDH-ES`, `ECDH-ES+A128KW`,
`ECDH-ES+A192KW` and `ECDH-ES+A256KW` names for key agreement over the `OKP` curves X25519 and X448. One name therefore
serves `EC` keys and `OKP` keys, with different sizes for each. The ECDH-ES implementation also hard-coded its
ephemeral key: it wrote `kty` as `EC` with `crv`, `x` and `y` into the `epk` header and refused any other `kty`, so no
package could contribute another key type to it although `NCode.Identity.Jose.EdDsa` already has `OKP` keys
([#327](https://github.com/NCodeGroup/Identity/issues/327)).

## Decision

1. **A keyed algorithm declares a list of key types.** `KeyedAlgorithm.KeyTypes` lists `AlgorithmKeyType` values, each
   a `SecretKey` CLR type with the sizes legal for keys of that type, in the algorithm's order of preference. A key is
   matched to the first entry its type derives from (`TryGetKeyType`), and its size is checked against that entry
   only. Most algorithms list one entry; the name is still registered once. An entry also lists the curves keys of
   its type must be on, each an `AlgorithmCurve` with its `crv` name and its key size, in the algorithm's order of
   preference, because two curves can share a size: P-256 and secp256k1, Ed25519 and X25519.
2. **The ECDH-ES algorithms take their key types from registered agreements.** `IEcdhKeyAgreement`, in
   `NCode.Identity.Jose.Abstractions`, performs the Diffie-Hellman agreement over the curves of one key type: it
   generates the ephemeral key pair on the recipient's curve and returns the shared secret `Z` and the ephemeral public
   key as a `SubjectPublicKeyInfo`, and on the recipient's side agrees from a `SecretKey`. The four ECDH-ES algorithms
   are a strategy collection over every registered agreement ([ADR-1001](1001-mediator-vs-dependency-injection-logic-classes.md),
   [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)), and the agreement is chosen per key, not per key
   type: a key is agreed by the one agreement whose `IsKeyCompatible` accepts it, and a key that two agreements accept
   is refused as ambiguous. Several agreements may therefore serve one key type, each over its own curves, so a package
   can add ECDH-ES over another curve of the `EC` key type, such as a Brainpool curve, without replacing the core's
   agreement. The algorithms' `KeyTypes` merge the agreements that serve one key type into one entry with all their
   sizes and curves, and their order does not depend on the order the packages were registered in: the core's `EC`
   agreement comes first, then the contributed ones by key type and agreement type name. The core registers the `EC`
   agreement over P-256, P-384 and P-521; a package that contributes a key type registers its agreement beside it. The
   Concat KDF
   stays in the algorithm and stays SHA-256 for every key type
   ([ADR-1126](1126-ecdh-es-concat-kdf-digest-is-sha-256-and-oracles-must-be-rfc-conforming.md)).
3. **The `epk` is a JSON Web Key of the recipient key's type, written and read through that type's descriptors.** The
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
4. **Consumers ask the algorithm, not a single type.** The JWE decryption key check, `IsKeyCompatible` and
   `ValidateKeyAlgorithm` match the key's type with `TryGetKeyType` and check the matched entry's sizes. Type and
   size do not show a key's curve, so `ValidateKeyAlgorithm` also checks a declared curve against the entry's curves
   and sizes, and, when the caller holds the key, as the client key importer and the management update do, asks the
   algorithm's own `IsKeyCompatible`: an Ed25519 key declared for `ECDH-ES` or a P-256 key declared for `ES256K` is
   refused when it is registered, not only when it is first used. The JWE decryption key check asks `IsKeyCompatible`
   too, so a decryption key on the wrong curve is reported as the caller's configuration error; the check reads only the
   configured key, never the token ([RFC 7516 §11.5](https://datatracker.ietf.org/doc/html/rfc7516#section-11.5)). A policy that
   needs a family of keys, such as the asymmetric keys a published key or a `private_key_jwt` assertion requires, asks
   `AcceptsOnly<TKey>()`, which holds when every declared key type derives from `TKey`.
5. **A tenant key may name its secret type.** `OpenIdTenantKeyOptions.SecretType`, when set, selects which of the
   algorithm's key types the key is generated as and must be one the algorithm accepts; unset, the key is the first
   declared key type that has a registered secret key type and can be provisioned, so a key type that cannot yields to
   the next. An unset size is the smallest that key type accepts. When the key type lists curves, the key is generated
   on the first of them with the key's size, so a 256-bit `OKP` key for `ECDH-ES` is an X25519 key and a 448-bit one an
   X448 key. A persisted key satisfies the specification only when its secret type is the specification's. The
   management API's create request, `ISecretValidator.ResolveCurve`, chooses an unnamed curve the same way.
6. **The capabilities document lists each secret type with its own sizes.** `AlgorithmCapabilityResource.KeyTypes`
   lists `{ secretType, keyBitSizes, curves }` per compatible secret type, in the algorithm's order of preference, and
   replaces the separate `secretTypes` and `keyBitSizes` lists, which could not say which sizes belong to which type.
   The management console bounds the key size by the chosen secret type's entry and offers only its curves.
7. **AEAD algorithms are not keyed algorithms.** An authenticated encryption algorithm's key is a content encryption
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
- **Infer the tenant key's type from its size alone.** Rejected: `EC` P-256 and `OKP` X25519 keys are both 256 bits.
- **One agreement per key type, failing composition on an overlap.** Rejected: the core's `EC` agreement serves only
  the NIST curves, so no package could add ECDH-ES over another `EC` curve without replacing it, which closes the curve
  vocabulary that ADR-1150 keeps open. Choosing the agreement by the key keeps it open and still never lets two
  agreements derive the same key's content key.
- **An explicit priority on each agreement.** Rejected: the order matters only for the key a tenant is provisioned
  with when nothing selects a type, and a fixed order (the core's RFC 7518 agreement first) settles it without a
  number every contributor would have to choose.

## Consequences

- A package adds ECDH-ES over its key type by registering an `IEcdhKeyAgreement` and its key type's descriptors; the
  algorithms, the decryption key check, tenant key provisioning and the capabilities document serve it unchanged.
- `KeyedAlgorithm.KeyType` and `KeyBitSizes` are gone; implementers declare `KeyTypes`, and callers that compared a
  single key type use `TryGetKeyType` or `AcceptsOnly<TKey>()`.
- `ECDH-ES+A*KW` generates its content encryption key at random and wraps it with the agreed key encryption key, as
  RFC 7518 §4.6 describes, instead of deriving it from a discarded agreement.
- The capabilities contract changes shape; the console reads `keyTypes`.
- A key whose curve the algorithm does not accept is refused when it is declared, imported or provisioned, not only
  when it is first used; a declaration that names neither the key's curve nor the key itself is still checked only by
  type and size.

## References

- [RFC 7518 §4.6](https://datatracker.ietf.org/doc/html/rfc7518#section-4.6),
  [RFC 8037 §3.2](https://datatracker.ietf.org/doc/html/rfc8037#section-3.2),
  [RFC 7748 §6](https://datatracker.ietf.org/doc/html/rfc7748#section-6),
  [RFC 7638 §3.2](https://datatracker.ietf.org/doc/html/rfc7638#section-3.2)
- [ADR-1001](1001-mediator-vs-dependency-injection-logic-classes.md),
  [ADR-1067](1067-secret-type-capability-catalog-and-capabilities-discovery.md),
  [ADR-1105](1105-secret-key-type-descriptor-first-class-seam.md),
  [ADR-1126](1126-ecdh-es-concat-kdf-digest-is-sha-256-and-oracles-must-be-rfc-conforming.md),
  [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md),
  [ADR-1169](1169-every-tenant-is-provisioned-with-persisted-keys.md),
  [ADR-1271](1271-a-key-type-registers-its-json-web-key-rules.md)
- Issue [#327](https://github.com/NCodeGroup/Identity/issues/327)
- `KeyedAlgorithm`, `AlgorithmKeyType` and `IEcdhKeyAgreement` in `NCode.Identity.Jose.Abstractions`;
  `EcdhKeyManagementAlgorithm` and `EccEcdhKeyAgreement` in `NCode.Identity.Jose`
