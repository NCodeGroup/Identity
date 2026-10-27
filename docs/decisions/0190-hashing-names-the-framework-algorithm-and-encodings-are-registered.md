# 190. Hashing names the framework algorithm, and binary encodings are registered strategies

- **Status:** Accepted
- **Date:** 2026-10-20
- **Deciders:** NCode Group

## Context

`ICryptoService` hashes values and encodes binary data as text for the subject identifier, the PKCE `S256` challenge,
the session state, the persisted-grant key, and every generated key or resource id. It chose the hash with a
`HashAlgorithmType` enum (`Sha1`, `Sha256`, `Sha512`, whose values were the digest lengths) and the text form with a
`BinaryEncodingType` enum (`Base64Url`, `Base64`, `Hex`). `DefaultCryptoService` dispatched both through a `switch`.

Neither set is closed under the three-part test of
[ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md). A token hash for a `*384` algorithm needs SHA-384,
which the enum lacked, and the framework already hashes SHA-3. A package that issues TOTP secrets or human-typed codes
needs Base32. Each member was fixed only because the implementation was a `switch`, and the hash enum also encoded the
digest length in its ordinal.

## Decision

1. **A hash algorithm is the framework's `HashAlgorithmName`.** `ICryptoService.HashValue` takes a
   `System.Security.Cryptography.HashAlgorithmName`, the open name vocabulary that RSA, ECDsa, and PBKDF2 already use.
   `DefaultCryptoService` computes the digest with `CryptographicOperations.TryHashData`, so every algorithm the
   framework implements works without a library change, and the digest length comes from the framework, not from an
   ordinal. An unknown name is rejected with the framework's `CryptographicException`. A deployment that needs an
   algorithm the framework does not implement replaces `ICryptoService`.
2. **A binary encoding is a name plus a registered `IBinaryEncoder`.** `BinaryEncodings` holds the built-in names
   `base64url`, `base64`, and `hex`. Each is the `Name` of an `IBinaryEncoder` in the DI strategy collection, and
   `AddIdentityLibrary` registers the three built-ins with `TryAddEnumerable`. `DefaultCryptoService` builds its
   name-to-encoder map when it is constructed, fails on a name registered twice, and rejects an unregistered name with
   an `ArgumentException` that names it. Names compare ordinally. A package adds an encoding, such as Base32, by
   registering another encoder.

The hashed and encoded output of every existing call site is unchanged, so no stored value needs a migration.

## Options considered

- **Keep both enums and add members.** Rejected: each new algorithm or encoding would still need a library change,
  which is the failure ADR-0150 describes.
- **A library-owned hash-algorithm registry.** Rejected: the framework already defines the open vocabulary and the
  implementation. A second registry would repeat it and could drift from what the framework supports.
- **Pass an encoder instance instead of a name.** Rejected: a name is what a setting or a stored record can carry, and
  resolving it through the registered collection keeps one place that validates the set.

## Consequences

- `HashAlgorithmType` and `BinaryEncodingType` are removed. `ICryptoService` takes a `HashAlgorithmName` and a
  `string` encoding name.
- SHA-384 and the SHA-3 family are available to every caller of `ICryptoService`.
- A mock of `ICryptoService` matches the encoding by name and the algorithm by `HashAlgorithmName` value equality.

## References

- [ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md) — vocabularies are open unless provably closed.
- [RFC 4648](https://www.rfc-editor.org/rfc/rfc4648) — the Base64, Base64url, and Base16 encodings.
