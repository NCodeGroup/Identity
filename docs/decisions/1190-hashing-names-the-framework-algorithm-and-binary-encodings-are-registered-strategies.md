# 1190. Hashing names the framework algorithm, and binary encodings are registered strategies

- **Status:** Accepted
- **Date:** 2026-10-20
- **Deciders:** NCode Group

## Context

`ICryptoService` hashes values and encodes binary data as text for the subject identifier, the PKCE `S256` challenge,
the session state, the persisted-grant key and every generated key or resource id.

Neither the set of hash algorithms nor the set of text encodings is closed under the three-part test of
[ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md). A token hash for a `*384` algorithm needs SHA-384, and
the framework already hashes the SHA-3 family. A package that issues TOTP secrets or human-typed codes needs Base32. A
closed set would fix each member only because the implementation is a `switch`, and an enum whose values are digest
lengths would encode the length in its ordinal. Both vocabularies are therefore open: the framework owns the algorithm
names, and a registered strategy owns each encoding.

## Decision

1. **A hash algorithm is the framework's `HashAlgorithmName`.** `ICryptoService.HashValue` takes a
   `System.Security.Cryptography.HashAlgorithmName`, the open name vocabulary that RSA, ECDsa and PBKDF2 already use.
   `DefaultCryptoService` computes the digest with `CryptographicOperations.TryHashData`, so every algorithm the
   framework implements works without a library change, SHA-384 and the SHA-3 family included, and the digest length
   comes from the framework, not from an ordinal. An unknown algorithm name fails with the framework's
   `CryptographicException`. A deployment that needs an algorithm the framework does not implement replaces
   `ICryptoService`.
2. **A binary encoding is a string name plus a registered `IBinaryEncoder` whose `Name` it is.** `BinaryEncodings`
   (`NCode.Identity.Cryptography.Abstractions`) holds the built-in names `base64url`, `base64` and `hex`, and
   `AddCryptographyLibrary` registers their encoders with `TryAddEnumerable`. `DefaultCryptoService` builds its ordinal
   name-to-encoder map when it is constructed, throws `InvalidOperationException` on a name registered twice, and rejects
   an unregistered name with an `ArgumentException` that names it.
3. **A package adds an encoding by registering another `IBinaryEncoder`.** Base32 for TOTP secrets or human-typed codes
   is such an encoder; no library change is needed.

## Options considered

- **Enums with more members.** Rejected: each added algorithm or encoding is still a library change, which is the
  failure [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md) describes.
- **A library-owned hash-algorithm registry.** Rejected: the framework already defines the open vocabulary and the
  implementation, so a second registry repeats it and can drift from what the framework supports.
- **Passing an encoder instance instead of a name.** Rejected: a setting or a stored record carries a name, and
  resolving it through the registered collection keeps one place that validates the set.
- **The framework's `HashAlgorithmName` and registered `IBinaryEncoder` strategies, chosen.** The algorithm vocabulary
  stays the framework's and the encoding vocabulary stays open to any package.

## Consequences

- `ICryptoService` takes a `HashAlgorithmName` and a `string` encoding name.
- SHA-384 and the SHA-3 family are available to every caller of `ICryptoService`.
- A mock of `ICryptoService` matches the encoding by name and the algorithm by `HashAlgorithmName` value equality.

## References

- [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md) — vocabularies are open unless provably closed.
- [RFC 4648](https://www.rfc-editor.org/rfc/rfc4648) — the Base64, Base64url and Base16 encodings.
