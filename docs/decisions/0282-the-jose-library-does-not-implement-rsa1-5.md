# 282. The JOSE library does not implement RSA1_5

- **Status:** Accepted
- **Date:** 2026-10-28
- **Deciders:** NCode Group

## Context

RFC 7518 §4.2 defines `RSA1_5`, key encryption with RSAES-PKCS1-v1_5. RFC 7516 §11.5 warns that an implementation which
reports a bad PKCS#1 v1.5 padding differently from a bad authentication tag is open to the Bleichenbacher attack, and
RFC 8725 §3.2 says to avoid every RSA-PKCS1 v1.5 encryption algorithm in favour of RSAES-OAEP.
draft-ietf-jose-deprecate-none-rsa15 asks libraries to deprecate `RSA1_5` and applications to disable it by default,
enabling it only "for the specific objects or operations that require it and not at a global level". It cites NIST
SP 800-131A Rev. 2, which disallows PKCS#1 v1.5 key transport for federal use.

A default algorithm registry that contains `RSA1_5` makes every direct consumer of `IJoseSerializer` decrypt a JWE that
names it. `IJwtValidator` accepts one as well whenever `JwtValidationParameters.ValidKeyManagementAlgorithms` is `null`,
which means "any registered key management algorithm". A malformed `RSA1_5` encrypted key surfaces as a
`CryptographicException`, while a bad authentication tag surfaces as a `JoseEncryptionException`, so a caller can tell
the two failures apart. That difference is the padding oracle, and it allows the recovery of a JWE's content key.

The OpenID provider and the protocol client are not exposed. `SettingAlgorithms.IsSelectable` and
`ClientDecryptionPolicy` refuse `RSA1_5` by name, and both validate with explicit allow-lists
([ADR-0155](0155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md),
[ADR-0280](0280-the-protocol-client-decrypts-id-and-logout-tokens-with-its-key-ring.md)). A consumer that uses the
Jose or Jwt packages directly, without an allow-list, is the one at risk.

## Decision

**The JOSE library implements no `RSA1_5` key encryption.**

1. `DefaultAlgorithmDataSource` does not register `RSA1_5`, and no registration method adds it. RSA key management is
   registered only as `RSA-OAEP`, `RSA-OAEP-256`, `RSA-OAEP-384` and `RSA-OAEP-512`. A consumer with a legacy peer
   implements its own `KeyManagementAlgorithm` and registers it through `ICollectionDataSource<Algorithm>`: the
   vocabulary stays open ([ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md)), and the risk is that
   consumer's explicit choice.
2. PKCS#1 v1.5 padding is unrepresentable. The internal `RsaOaepKeyManagementAlgorithm(string code, HashAlgorithmName
   hashAlgorithmName)` builds `RSAEncryptionPadding.CreateOaep(hashAlgorithmName)`. It is named by its scheme like
   `Pbes2KeyManagementAlgorithm` and `AesGcmKeyManagementAlgorithm`, and it has no PKCS#1 branch.
3. The name stays. `AlgorithmCodes.KeyManagement.RsaPkcs1` (`"RSA1_5"`) is the IANA-registered `alg` value that policies
   refuse by name.
4. The OpenID guards stay. `SettingAlgorithms.IsSelectable` and
   `ClientDecryptionPolicy.GetPermittedKeyManagementAlgorithms` keep refusing `RSA1_5` by name, so a host that registers
   its own `RSA1_5` never makes the provider advertise it (the algorithm settings derive their defaults from the
   registered algorithms) and never makes the protocol client decrypt with it. Their tests register a viable `RSA1_5`
   and pair it with an `RSA-OAEP-256` control, so each passes only because of the name rule.
5. There is no special message. A JWE that names `RSA1_5` fails like any unregistered algorithm, with a
   `JoseInvalidAlgorithmException`: "The `RSA1_5` algorithm is not supported for key management."

## Options considered

- **Keep it registered with a documented warning.** The library stays compatible with every `RSA1_5` peer, but the
  oracle remains in every default composition. RFC 8725 §3.2 and the draft ask for the opposite default. Rejected.
- **An opt-in registration method.** The default composition is safe, but the library keeps an implementation that
  nobody should use, which would also need the RFC 7516 §11.5 hardening (a random content key substituted on a failed
  unwrap). A registration method is also a global switch, and the draft allows enabling `RSA1_5` only for the specific
  objects or operations that require it. Rejected.
- **Remove it.** The default and every composition are safe, the RSA key management class cannot express PKCS#1 v1.5,
  and the open vocabulary ([ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md)) leaves a consumer with a
  legacy need free to supply its own algorithm. Chosen.

## Consequences

- A consumer that exchanges `RSA1_5` JWEs moves to `RSA-OAEP-256` or `ECDH-ES`.
- A legacy need is met by the consumer's own registered `KeyManagementAlgorithm`. That is a global choice and the
  consumer's explicit risk, and the provider's algorithm settings and the protocol client still refuse `RSA1_5` by
  name.
- `RsaOaepKeyManagementAlgorithm` cannot express PKCS#1 v1.5 padding.
- `none`, which the same draft deprecates, is a separate decision.

## References

- [ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md)
- [ADR-0155](0155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md)
- [ADR-0280](0280-the-protocol-client-decrypts-id-and-logout-tokens-with-its-key-ring.md)
- RFC 7516 §11.5 (timing attacks and Bleichenbacher)
- RFC 7518 §4.2–4.3 (RSAES-PKCS1-v1_5 and RSAES-OAEP key encryption)
- RFC 8725 §3.2 (avoid all RSA-PKCS1 v1.5 encryption algorithms)
- draft-ietf-jose-deprecate-none-rsa15 (deprecating `none` and `RSA1_5`)
- NIST SP 800-131A Rev. 2 (transitioning the use of cryptographic algorithms and key lengths)
- Issue #339
