# 271. A key type registers its JSON Web Key rules

- **Status:** Accepted
- **Date:** 2026-10-27
- **Deciders:** NCode Group

## Context

`JsonWebKey` computed a thumbprint and detected private key material from two hard-coded tables: one `switch` on `kty`
for the RFC 7638 thumbprint members, and one set of private member names. Both knew only the standard key types. A key
type contributed through `ISecretKeyTypeCatalog` therefore could not be thumbprinted, and DPoP validation failed for a
contributed `kty`: `DPoPProof.PublicKey.ComputeThumbprint()` threw `NotSupportedException`, although the catalog already
imported and exported the key. [ADR-0119](0119-first-class-jwk-jwks-and-thumbprint.md) claimed that a custom key type's
thumbprint "falls out of the same generic path"; that was wrong, because the generic path ended at the table.

The AKP table was also wrong. RFC 9964 §6 defines the AKP thumbprint members as `alg`, `kty` and `pub`, and the table
listed `kty` and `pub` only.

## Decision

**A key type's JWK rules are registered data carried by the key, not a table inside `JsonWebKey`.**

1. **A descriptor type.** A key type's JWK rules are a sealed data type,
   `JsonWebKeyTypeDescriptor(name, thumbprintMembers, privateMembers)`, in `NCode.Identity.Jwk`, per
   [ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md) (an open vocabulary is names plus registered data).
   A static registry or a DI service in the Jwk package was rejected: the Jwk package is DI-free, and
   `ISecretKeyTypeCatalog` is already the registry of key types.
2. **Standard descriptors.** The standard types RSA, EC, OKP, oct and AKP are built-in descriptors in
   `JsonWebKeyTypeDescriptors`, with `TryGetStandard`. Their membership is fixed by the IANA JWK Key Types registry. The
   open path is the typed constructor.
3. **A key carries its descriptor.** A `JsonWebKey` exposes `KeyTypeDescriptor`. The existing constructors resolve a
   standard descriptor or `null`; new constructors take a descriptor explicitly and throw `ArgumentException` when its
   `Name` differs from `kty`. This was chosen over descriptor-taking overloads of `ComputeThumbprint`,
   `ContainsPrivateKeyMaterial` and `ToPublicKey`, because a typed key keeps working downstream:
   `DPoPProof.PublicKey.ComputeThumbprint()` works for a contributed type.
4. **Thumbprint.** `ComputeThumbprint` uses `KeyTypeDescriptor.ThumbprintMembers` and throws `NotSupportedException` when
   the key has no descriptor (unchanged behavior for an unknown `kty`).
5. **Private-member detection fails closed.** The private member names of every standard type
   (`d`, `p`, `q`, `dp`, `dq`, `qi`, `oth`, `k`, `priv`) are always treated as private, plus the descriptor's own
   `PrivateMembers`. A contributed type therefore cannot use a standard private name for a public member. This keeps
   today's rejections, such as an RSA key carrying `k`.
6. **The catalog descriptor.** `ISecretKeyTypeDescriptor.JwkKeyType` changes from `string?` to
   `JsonWebKeyTypeDescriptor?`. The property name stays, so it still lines up with
   `ISecretKeyTypeCatalog.TryGetByJwkKeyType(string)`. The catalog indexes by `JwkKeyType.Name`.
7. **Consumers construct typed keys.** Consumers that resolve a key type through the catalog (DPoP validation,
   `DefaultClientKeyImporter`, `ProviderKeySetImporter`) construct the typed key. The descriptors themselves construct
   typed keys on export and import.
8. **AKP correction.** The AKP thumbprint members become `alg`, `kty` and `pub` (RFC 9964 §6). The AKP descriptor always
   exports `alg` and requires it on import, so no stored data or flow breaks.
9. **Thumbprint member normalization.** `ThumbprintMembers` always includes `kty` (RFC 7638 §3.2), added by the
   constructor when not listed, and is sorted ordinally (RFC 7638 §3.3 lexicographic order).
10. **Amendment.** These rules are recorded here, and this ADR amends ADR-0119.

## Options considered

- **Overloads that take a descriptor** (`ComputeThumbprint(descriptor)`, `ContainsPrivateKeyMaterial(descriptor)`,
  `ToPublicKey(descriptor)`). Rejected: every caller must find the descriptor again, and a key that crosses a boundary,
  such as the DPoP proof's public key, loses its rules.
- **A static mutable registry** in the Jwk package. Rejected: global mutable state with registration-order and test
  isolation problems, and it duplicates the catalog.
- **A DI service in the Jwk package.** Rejected: the Jwk package is DI-free, and `ISecretKeyTypeCatalog` already is the
  registry.

## Consequences

- A contributed key type supplies a `JsonWebKeyTypeDescriptor` through its `ISecretKeyTypeDescriptor`, and its keys
  thumbprint, validate and pass DPoP validation with no library change.
- `ISecretKeyTypeDescriptor.JwkKeyType` is a breaking change to the property's type, accepted by the pre-release posture
  ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)).
- An AKP key thumbprint now includes `alg`, so it differs from the earlier value, and matches RFC 9964.
- A key built without a descriptor and with an unknown `kty` still cannot be thumbprinted.

## References

- [ADR-0119](0119-first-class-jwk-jwks-and-thumbprint.md) — first-class JWK, JWKS and thumbprint (amended)
- [ADR-0105](0105-secret-key-type-descriptor-first-class-seam.md) — the secret key type descriptor seam
- [ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md) — open vocabularies
- RFC 7638 — JSON Web Key (JWK) Thumbprint
- RFC 9964 — JOSE and COSE Algorithm Key Pair (AKP) key type
- `src/NCode.Identity.Jwk/`
