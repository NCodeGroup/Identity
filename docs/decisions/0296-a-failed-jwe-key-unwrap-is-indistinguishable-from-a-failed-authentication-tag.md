# 296. A failed JWE key unwrap is indistinguishable from a failed authentication tag

- **Status:** Accepted
- **Date:** 2026-10-29
- **Deciders:** NCode Group

## Context

RFC 7516 §11.5 requires a JWE recipient not to distinguish between format, padding and length errors of encrypted
keys, and strongly recommends substituting a randomly generated content encryption key (CEK) when the encrypted key
cannot be unwrapped, so the decryption fails at the authentication tag and its timing reveals nothing either. The
attacks it guards against (RFC 3218, Bleichenbacher) turn any observable difference between "the key did not unwrap"
and "the tag did not verify" into an oracle on the key encryption.

`JoseSerializer` reported a failed unwrap as "Failed to decrypt the encrypted content encryption key (CEK)." and a failed
tag as "Failed to decrypt the JWE Ciphertext.", and let a `CryptographicException` or an `ArgumentException` from a key
management algorithm (a bad AES key wrap integrity value, an encrypted key of the wrong length) escape
`IJoseSerializer.Decode` with its own type and message. A caller, or a log a caller exposes, could tell them apart.
[ADR-0282](0282-the-jose-library-does-not-implement-rsa1-5.md) removed `RSA1_5`, the classic padding oracle, but the
difference remained for every other key management algorithm.

## Decision

**Every failure of a JWE decryption that depends on the encrypted key, the ciphertext or the tag is one
`JoseEncryptionException` with the message "Failed to decrypt the JWE." and no inner exception; a key that does not fit
the algorithm is still reported as the caller's configuration error.** It applies to the compact serialization and to
every recipient of the JSON serialization.

1. **The key is checked first.** Before unwrapping, the recipient's key must be of the algorithm's `KeyType` and of a
   legal `KeyBitSizes` size; otherwise an `ArgumentException` reports the configuration error, as before. These depend
   only on the caller's key, never on the token.
2. **A failed unwrap substitutes a random CEK.** When the key management algorithm returns `false`, writes a CEK whose
   length is not the content encryption algorithm's, or throws a `CryptographicException`, a `JoseException` or an
   `ArgumentException`, the CEK buffer is filled from `RandomNumberGenerator` and decryption proceeds.
3. **The tag decides.** The content encryption algorithm then fails to verify the tag, as it does for any tampered JWE.
   A `false` result or an exception from it is reported with the one message and no inner exception, so no cause is
   observable from the exception.

## Options considered

- **Keep distinct messages.** They help debugging, but they are the oracle RFC 7516 §11.5 forbids. Rejected.
- **One message, but keep the inner exception.** The inner exception's type and message still distinguish the causes
  to anything that logs or serializes it. Rejected.
- **Throw at the unwrap with the uniform message, without a random CEK.** The message is uniform, but the failure
  returns earlier than a tag failure, so timing distinguishes them. Rejected.

## Consequences

- A tampered encrypted key, a wrong-length encrypted key and a tampered tag fail identically.
- Diagnosing a misconfigured recipient relies on the key-type and key-size checks, which still report precisely.
- A contributed key management algorithm needs nothing new: its unwrap failures are absorbed the same way.

## References

- [ADR-0280](0280-the-protocol-client-decrypts-id-and-logout-tokens-with-its-key-ring.md) — decryption in the protocol
  client
- [ADR-0282](0282-the-jose-library-does-not-implement-rsa1-5.md) — no `RSA1_5`
- RFC 7516 §11.5 (timing attacks)
- RFC 3218 (preventing the million message attack on CMS)
- Issue #380
