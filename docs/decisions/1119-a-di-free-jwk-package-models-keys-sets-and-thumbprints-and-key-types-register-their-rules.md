# 1119. A DI-free JWK package models keys, sets and thumbprints, and key types register their rules

- **Status:** Accepted
- **Date:** 2026-10-28
- **Deciders:** NCode Group

## Context

Issue [#72](https://github.com/NCodeGroup/identity/issues/72) asks for first-class **JSON Web Key** (RFC 7517),
**JSON Web Key Set** (RFC 7517 §5) and **JWK Thumbprint** / **Thumbprint URI** (RFC 7638 / RFC 9278) types. Two
constraints shape the design, and they pull against each other:

1. **`*.Abstractions` packages contain contracts only, no logic.** A JWK that the codebase uses (verify a proof,
   produce members, hash a canonical form) needs behavior, and behavior does not live in an abstractions package.
2. **JWK is first-class in both the Secrets and the JOSE stacks.** `Secrets.Abstractions` is the lowest shared layer
   (`Jose.Abstractions` depends on it), and the descriptor seam that produces JWK members lives in Secrets. A single
   shared JWK type therefore cannot live in `Jose.Abstractions`, and a behavior-bearing type in
   `Secrets.Abstractions` breaks the first constraint.

Two vocabularies of the JWK domain are open. A key type's thumbprint members and private members differ per `kty`:
the standard types are fixed by the IANA JWK Key Types registry, and a key type contributed through
`ISecretKeyTypeCatalog` needs the same treatment, or it cannot be thumbprinted and DPoP validation fails for its `kty`.
The hash name in a JWK Thumbprint URI comes from the IANA Named Information Hash Algorithm Registry (RFC 9278 §4),
which grows over time and which the package cannot list in full, while the framework also computes SHA-3. A package
without dependency injection cannot reach a registry, so each open vocabulary needs a channel from the caller.

## Decision

**A dedicated, logic-bearing, DI-free package models the JWK, the key set and the thumbprint. A key type's JWK rules
are registered data that a key carries, and where the package meets an open vocabulary it ships the registered members
as built-in data and takes any other member from its caller as an argument.**

1. **A dedicated package.** `NCode.Identity.Jwk` holds `JsonWebKey`, `JsonWebKeySet`, `JsonWebKeyParameterNames`,
   `JsonWebKeyTypeDescriptor` and `JsonWebKeyTypeDescriptors`. JWK, JWKS and thumbprint are a self-contained RFC domain
   (7517 / 7638 / 9278), distinct from JOSE signing and encryption (7515 / 7516) and from key storage (Secrets).
   `NCode.Identity.Secrets.Abstractions` and `NCode.Identity.Jose.Abstractions` both reference it. Referencing a model
   library is fine: no abstractions package contains its logic.
2. **An immutable `JsonElement` backs the key.** `JsonWebKey` is backed by a detached, immutable `JsonElement` that
   owns no `JsonDocument`, so there is nothing to dispose or defensively clone.
3. **Constructors adopt, `Parse` reads text.** A constructor adopts an in-memory value: `JsonWebKey(JsonElement)`,
   `JsonWebKey(JsonObject)`, `JsonWebKeySet(JsonElement)` and `JsonWebKeySet(IEnumerable<JsonWebKey>)`. The static
   `Parse(string)` reads text.
4. **The thumbprint is a method on the key, not a service.** `ComputeThumbprint()` and
   `ComputeThumbprint(HashAlgorithmName)` compute the RFC 7638 thumbprint, and `ComputeThumbprintUri(...)` the RFC 9278
   URI. Every user shares that one representation: `ExportedPublicKey.Jwk`, the JWKS endpoint's `JsonWebKeySet`, the
   management public-key read (`AsElement()`) and DPoP (`DPoPProof.PublicKey.ComputeThumbprint()`).
5. **`JsonWebKey` is one generic wire model.** The secret key type descriptor
   ([ADR-1104](1104-a-key-type-is-one-registered-descriptor-and-public-keys-are-imported-and-stored-by-encoding.md))
   still produces each key type's members. `JsonWebKey` wraps them and adds parse, serialize and thumbprint; it is not
   a per-key-type converter registry.
6. **A key type's JWK rules are registered data.** `JsonWebKeyTypeDescriptor(name, thumbprintMembers, privateMembers)`
   is a sealed data type, per [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md): an open vocabulary is
   names plus registered data. The standard RSA, EC, OKP, oct and AKP descriptors are built into
   `JsonWebKeyTypeDescriptors` and found with `TryGetStandard`. The AKP thumbprint members are `alg`, `kty` and `pub`
   (RFC 9964 §6), which the AKP descriptor always exports and requires on import. `ThumbprintMembers` always includes
   `kty`, which the constructor adds when it is not listed (RFC 7638 §3.2), and is sorted ordinally (RFC 7638 §3.3).
7. **A key carries its descriptor.** `JsonWebKey` exposes `KeyTypeDescriptor`. The untyped constructors resolve the
   standard descriptor or `null`. The typed constructors take a descriptor and throw `ArgumentException` when its
   `Name` differs from `kty`. `ComputeThumbprint` uses `KeyTypeDescriptor.ThumbprintMembers` and throws
   `NotSupportedException` for a key without one.
8. **Private-member detection fails closed.** `ContainsPrivateKeyMaterial` and `ToPublicKey` always treat the standard
   private names `d`, `p`, `q`, `dp`, `dq`, `qi`, `oth`, `k` and `priv` as private, plus the descriptor's
   `PrivateMembers`. A contributed type cannot use a standard private name for a public member, so an RSA key carrying
   `k` is still rejected.
9. **The catalog indexes by descriptor.** `ISecretKeyTypeDescriptor.JwkKeyType` is a `JsonWebKeyTypeDescriptor?`, and
   `ISecretKeyTypeCatalog.TryGetByJwkKeyType` indexes by its `Name`. The descriptors construct typed keys on export and
   import. DPoP validation, `DefaultClientKeyImporter` and `ProviderKeySetImporter` resolve the key type through the
   catalog and build typed keys.
10. **A typed set.** `JsonWebKeySet(JsonElement, IEnumerable<JsonWebKeyTypeDescriptor>)` and
    `JsonWebKeySet.Parse(string, descriptors)` read a set with the caller's descriptors; `JsonWebKeySet(JsonElement)`
    and `Parse(string)` delegate with none. A key whose `kty` ordinally equals a supplied descriptor's `Name` uses that
    descriptor, even for a standard `kty`. Any other key uses its standard descriptor or none. A null entry, or two
    descriptors with one `Name`, throw `ArgumentException`: the descriptors are validated as a whole, like
    `DefaultSecretKeyTypeCatalog`.
11. **A key without a usable `kty` is skipped.** Reading a set skips a key whose `kty` is missing, not a string or
    empty, by the exact test `JsonWebKey` applies when it reads its key type, so one malformed key never poisons the
    set.
12. **The attribute-bound converter is standard-only.** `JsonWebKeySetJsonConverter`, bound to `JsonWebKeySet` by
    attribute, types keys with the standard descriptors only, because a type-level converter has no DI-free channel for
    descriptors. The class remarks and the Jwk README state it. The importers read sets as `JsonElement` and need each
    key's `ISecretKeyTypeDescriptor` as well, so they do not use it.
13. **A built-in map names the hash.** A private map holds every IANA registry entry whose whole digest a framework
    `HashAlgorithmName` computes: `SHA256` is `sha-256`, `SHA384` is `sha-384`, `SHA512` is `sha-512`, `SHA3-256` is
    `sha3-256`, `SHA3-384` is `sha3-384` and `SHA3-512` is `sha3-512`. The keys are the `HashAlgorithmName.Name` values,
    written as string literals because `Name` is `string?`. `ComputeThumbprintUri(HashAlgorithmName)` uses the map and
    throws `NotSupportedException` for any other hash, with a message that points to the overload.
14. **The open path is an overload.** `ComputeThumbprintUri(HashAlgorithmName, string hashName)` takes the registry name
    from the caller. It requires `1*unreserved`, the `alg` rule of RFC 6920 §3 (ASCII letters, digits, `-`, `.`, `_`
    and `~`), and for a hash the map knows it requires the mapped name, so a known hash is never mislabeled. It cannot
    check that another name is registered; that is the openness the overload exists for.
15. **Only whole digests are named.** Truncated registry names such as `sha-256-128` are never produced: an RFC 7638
    thumbprint is the whole digest, and a truncated key identifier weakens the binding. `sha3-224`, `blake2*` and
    `k12-*` are not mapped, because the package hashes with `IncrementalHash`, which computes only framework algorithms
    ([ADR-1190](1190-hashing-names-the-framework-algorithm-and-binary-encodings-are-registered-strategies.md)). A caller
    whose runtime computes one of them passes its name to the overload.

## Options considered

- **A data-only JWK DTO with a thumbprint service.** Rejected: a DTO is not a usable working type, so the descriptors
  keep building `JsonObject` and consumers keep parsing by hand, converting to the DTO only at the serialization
  edges.
- **A static registry or a DI service in the Jwk package.** Rejected: the package is DI-free, `ISecretKeyTypeCatalog`
  is already the registry of key types, and a static mutable registry is global state with registration-order and
  test-isolation problems.
- **Descriptor-taking overloads of `ComputeThumbprint`, `ContainsPrivateKeyMaterial` and `ToPublicKey`.** Rejected:
  every caller must find the descriptor again, and a key that crosses a boundary, such as the DPoP proof's public key,
  loses its rules.
- **A `WithKeyTypes` method on `JsonWebKeySet`.** Rejected: it types keys after the fact, so it builds each key twice,
  and a caller can hold an untyped set by mistake.
- **A public converter configured with descriptors.** Rejected: more public surface, and no caller in the family needs
  it, because the importers read sets as `JsonElement`.
- **Deriving the hash name from `HashAlgorithmName.Name`**, rewriting `SHA3-256` to `sha3-256`. Rejected, per
  [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md): no trait is inferred from a name. The map states
  each registry name, and a name that does not match the registry fails rather than guessing.
- **A public constant class of registry names.** Rejected: no caller compares them, and the overload takes any name.
- **A dedicated DI-free package with registered key type data, a typed set and a hash-name overload.** Chosen: it
  keeps every abstractions package free of logic, gives the concept one representation, and lets a caller supply what
  the package cannot know.

## Consequences

- The JWK concept has one home and one representation used end to end: the exported public key, the JWKS endpoint, the
  management read and DPoP share it. The package joins the family's lockstep versioning
  ([ADR-1003](1003-the-family-versions-in-lockstep-from-one-version-json-and-ships-source-linked-symbol-packages.md)).
- A contributed key type supplies a `JsonWebKeyTypeDescriptor` through its `ISecretKeyTypeDescriptor`, and its keys
  thumbprint, are screened for private members and pass DPoP validation with no library change. Keys of a contributed
  type read from a set with its descriptors behave the same.
- The AKP thumbprint matches RFC 9964, because it includes `alg`.
- A SHA-3 thumbprint URI is produced for `SHA3-256`, `SHA3-384` and `SHA3-512` where the runtime supports SHA-3, and a
  hash the framework adds later works through the overload.
- A set deserialized through `System.Text.Json` uses the standard descriptors only; a caller with contributed key
  types parses with the descriptor overloads.
- A key built without a descriptor and with an unknown `kty` cannot be thumbprinted.
- A caller that passes a wrong but well-formed name for an unknown hash is not caught, because the package cannot check
  registry membership.

## References

- Issues [#72](https://github.com/NCodeGroup/identity/issues/72), [#328](https://github.com/NCodeGroup/identity/issues/328)
  and [#329](https://github.com/NCodeGroup/identity/issues/329)
- [ADR-1104](1104-a-key-type-is-one-registered-descriptor-and-public-keys-are-imported-and-stored-by-encoding.md) — the
  secret key type descriptor seam
- [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md) — open vocabularies
- [ADR-1190](1190-hashing-names-the-framework-algorithm-and-binary-encodings-are-registered-strategies.md) — hashing
  names the framework algorithm
- RFC 7517 §5 — JWK Set Format
- RFC 7638 — JSON Web Key (JWK) Thumbprint
- RFC 9278 §4 — JWK Thumbprint URI, hash names
- RFC 6920 §3 — Naming Things with Hashes, URI format
- RFC 9964 §6 — AKP key type thumbprint
- <https://www.iana.org/assignments/named-information> — Named Information Hash Algorithm Registry
- `src/NCode.Identity.Jwk/`
