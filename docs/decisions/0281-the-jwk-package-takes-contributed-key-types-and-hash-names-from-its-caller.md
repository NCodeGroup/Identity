# 281. The JWK package takes contributed key types and hash names from its caller

- **Status:** Accepted
- **Date:** 2026-10-28
- **Deciders:** NCode Group

## Context

`NCode.Identity.Jwk` is DI-free ([ADR-0119](0119-first-class-jwk-jwks-and-thumbprint.md),
[ADR-0271](0271-a-key-type-registers-its-json-web-key-rules.md)), yet two of its vocabularies are open.

A `JsonWebKeySet` built every key with the standard descriptor of its `kty` only. A key of a contributed key type, read
from a set, therefore had no `JsonWebKeyTypeDescriptor`: it could not be thumbprinted, and its private members went
unseen by `ContainsPrivateKeyMaterial` and `ToPublicKey`. `new JsonWebKey(element, descriptor)` types a single key, but
a set had no way to carry the descriptors to its keys.

The hash name in a JWK Thumbprint URI came from a closed `switch` of three SHA-2 hashes. RFC 9278 §4 takes the name from
the IANA Named Information Hash Algorithm Registry, and the framework also computes SHA-3, so a SHA-3 thumbprint URI
could not be produced. The registry grows over time, and the package cannot list names it does not know.

## Decision

**Where the DI-free JWK package meets an open vocabulary, it ships the registered members as built-in data and takes any
other member from its caller as an argument.**

1. **A built-in map of hash names.** A private map holds every entry of the IANA registry whose full digest a framework
   `HashAlgorithmName` computes: `SHA256` is `sha-256`, `SHA384` is `sha-384`, `SHA512` is `sha-512`, `SHA3-256` is
   `sha3-256`, `SHA3-384` is `sha3-384` and `SHA3-512` is `sha3-512`. The keys are the `HashAlgorithmName.Name` values,
   which use a hyphen for SHA-3. They are string literals, not `HashAlgorithmName.SHA3_256.Name`, because `Name` is
   `string?`. `ComputeThumbprintUri(HashAlgorithmName)` uses the map and throws `NotSupportedException` for any other
   hash, with a message that points to the overload below.
2. **The open path is an overload.** `ComputeThumbprintUri(HashAlgorithmName, string hashName)` takes the registry name
   from the caller. It requires `hashName` to be `1*unreserved`, the `alg` rule of RFC 6920 §3 (ASCII letters, digits,
   `-`, `.`, `_` and `~`), so the URN stays unambiguous. For a hash the map knows, it also requires the mapped name, so
   a known hash is never mislabeled. It cannot check that another name is registered; that is the openness the overload
   exists for.
3. **Only whole digests of framework hashes are named.** Truncated registry names (`sha-256-128` and the like) are never
   produced: an RFC 7638 thumbprint is the whole digest, and a truncated key identifier weakens the binding. `sha3-224`,
   `blake2*` and `k12-*` are not mapped, because the package hashes with `IncrementalHash`, which computes only
   framework algorithms ([ADR-0190](0190-hashing-names-the-framework-algorithm-and-encodings-are-registered.md)). A
   caller whose runtime computes one of them passes its name to the overload.
4. **A typed set.** `JsonWebKeySet(JsonElement, IEnumerable<JsonWebKeyTypeDescriptor>)` and
   `JsonWebKeySet.Parse(string, IEnumerable<JsonWebKeyTypeDescriptor>)` read a set with the caller's descriptors.
   A constructor adopts an element and `Parse` reads text, as in ADR-0119. `JsonWebKeySet(JsonElement)` and
   `Parse(string)` delegate with no descriptors.
5. **Which descriptor a key uses.** A key whose `kty` equals the `Name` of a supplied descriptor (an ordinal comparison)
   uses it, even for a standard `kty`, as `new JsonWebKey(element, descriptor)` allows. Any other key uses its standard
   descriptor, or none. A null entry, or two descriptors with one `Name`, throw `ArgumentException`: the set of
   descriptors is validated as a whole, like `DefaultSecretKeyTypeCatalog`.
6. **The attribute-bound converter stays standard-only.** `JsonWebKeySetJsonConverter`, bound to `JsonWebKeySet` by
   attribute, is unchanged: a type-level converter has no DI-free channel for descriptors. A set deserialized through
   `System.Text.Json` types its keys with the standard descriptors only, which the class remarks and the Jwk README
   state. The importers read sets as `JsonElement` and need each key's `ISecretKeyTypeDescriptor` as well, so they stay
   as they are.
7. **A key without a usable `kty` is skipped.** Reading a set skips a key whose `kty` is missing, not a string, or
   empty, by the exact test `JsonWebKey` applies when it reads its key type. Such a key would otherwise reach the
   `JsonWebKey` constructor, which throws `ArgumentException` and fails the whole set, against the intent that one
   malformed key cannot poison a set.

## Options considered

- **A static mutable registry** of descriptors or hash names in the Jwk package. Rejected, as in ADR-0271: global
  mutable state with registration-order and test-isolation problems.
- **A DI service in the Jwk package.** Rejected, as in ADR-0271: the Jwk package is DI-free.
- **A re-typing method on `JsonWebKeySet`** (`WithKeyTypes(descriptors)`). Rejected: it types keys after the fact, so it
  builds each key twice, and a caller can hold an untyped set by mistake.
- **A public converter configured with descriptors.** Rejected: more public surface, and no caller in the package family
  needs it, because the importers read sets as `JsonElement`.
- **Deriving the hash name from `HashAlgorithmName.Name`**, rewriting `SHA3-256` to `sha3-256`. Rejected, per
  [ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md): no trait is inferred from a name. The map states
  each registry name, and a name that does not match the registry fails rather than guessing.
- **A public constant class of registry names.** Rejected: no caller compares them, and the overload takes any name.

## Consequences

- Keys of a contributed type, read from a set with its descriptors, are thumbprinted and screened for private members
  with no library change.
- A SHA-3 JWK Thumbprint URI is produced for `SHA3-256`, `SHA3-384` and `SHA3-512`, where the runtime supports SHA-3.
- A hash the framework adds later works through the overload without a library change.
- A set deserialized through `System.Text.Json` uses the standard descriptors only. A caller with contributed key types
  parses with the descriptor overloads instead.
- A caller that passes a wrong but well-formed name for an unknown hash is not caught, because the package cannot check
  registry membership.
- ADR-0119 and ADR-0271 keep their status: nothing they decided changes.

## References

- [ADR-0119](0119-first-class-jwk-jwks-and-thumbprint.md) — first-class JWK, JWKS and thumbprint
- [ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md) — open vocabularies
- [ADR-0190](0190-hashing-names-the-framework-algorithm-and-encodings-are-registered.md) — hashing names the framework
  algorithm
- [ADR-0271](0271-a-key-type-registers-its-json-web-key-rules.md) — a key type registers its JSON Web Key rules
- RFC 7517 §5 — JWK Set Format
- RFC 7638 — JSON Web Key (JWK) Thumbprint
- RFC 9278 §4 — JWK Thumbprint URI, hash names
- RFC 6920 §3 — Naming Things with Hashes, URI format
- <https://www.iana.org/assignments/named-information> — Named Information Hash Algorithm Registry
- `src/NCode.Identity.Jwk/JsonWebKey.cs`
- `src/NCode.Identity.Jwk/JsonWebKeySet.cs`
- Issues [#328](https://github.com/NCodeGroup/Identity/issues/328) and
  [#329](https://github.com/NCodeGroup/Identity/issues/329)
