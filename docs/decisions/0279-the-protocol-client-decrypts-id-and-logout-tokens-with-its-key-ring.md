# 279. The protocol client decrypts ID and logout tokens with its key ring

- **Status:** Accepted
- **Date:** 2026-10-28
- **Deciders:** NCode Group

## Context

[ADR-0273](0273-the-protocol-client-validates-id-tokens-under-an-asymmetric-algorithm-policy.md) (point 8) and
[ADR-0274](0274-the-protocol-client-validates-logout-tokens-and-rejects-replays.md) (point 8) refuse an encrypted ID
token and an encrypted logout token: the validators pass no decryption keys, so the JWT validator rejects a JWE
([ADR-0154](0154-jwts-are-validated-as-signed-tokens-under-explicit-algorithm-and-key-policy.md)).

The NCode OP encrypts both kinds of token to a client that registers `id_token_encrypted_response_alg`
(`ClientJwtProtectionSettings.IdToken`, used by `DefaultBackChannelLogoutNotifier` for the logout token). A protocol
client that registered encryption therefore cannot validate any token the OP sends it. OpenID Connect Core §3.1.3.7
step 1 requires the client to decrypt an ID token with the key and algorithms it registered, and OpenID Connect
Back-Channel Logout 1.0 §2.6 step 1 requires the same for a logout token (issue #347).

The conventions require a received JWT to be validated only through `IJwtValidator` under an explicit allow-list of
algorithms and keys ([ADR-0154](0154-jwts-are-validated-as-signed-tokens-under-explicit-algorithm-and-key-policy.md)).
Decryption keys are the client's own private keys, which the client holds in a named key ring
([ADR-0270](0270-the-openid-protocol-client-uses-the-familys-jose-stack-and-named-key-rings.md)).

## Decision

**The protocol client decrypts an encrypted ID token or logout token with the asymmetric keys of the client's key
ring, under the `alg` and `enc` values the client registered, and requires every token to be encrypted when the client
registered encryption.**

1. **The registration lists the encryption algorithms.** `ClientRegistration.IdTokenKeyManagementAlgorithms`
   (`id_token_encrypted_response_alg`) and `IdTokenContentEncryptionAlgorithms` (`id_token_encrypted_response_enc`) are
   lists, like `IdTokenSigningAlgorithms`. A non-empty key management list means the client registered encryption. An
   empty content encryption list means `A128CBC-HS256`, the registration default (OpenID Connect Dynamic Client
   Registration §2, as the NCode OP does). A content encryption list without a key management list throws
   `InvalidOperationException`, a configuration error
   ([ADR-0270](0270-the-openid-protocol-client-uses-the-familys-jose-stack-and-named-key-rings.md) point 10).
2. **Only asymmetric key management is permitted.** The permitted key management algorithms are the registered ones
   whose key type is `AsymmetricSecretKey` (RSA-OAEP\*, ECDH-ES\*): never `RSA1_5` (RFC 8725 §3.2), never `dir`, AES key
   wrap or PBES2, because the OP encrypts only to a public key the client published (it mirrors the OP's
   `SettingAlgorithms.IsAsymmetricKeyManagement`). The permitted content encryption algorithms are the registered
   `AuthenticatedEncryptionAlgorithm`s that the registration names. Compression is never permitted
   (`ValidCompressionAlgorithms = []`, RFC 8725 §3.6). The provider's `id_token_encryption_*_values_supported` metadata
   is not consulted: the registration fixes the values.
3. **Encryption is all or nothing.** When the client registered no encryption, the key management and content
   encryption lists are empty and no decryption keys are named, so a JWE fails with `algorithm_not_permitted`. When it
   registered encryption, a plain JWS fails with `invalid_token`, as the specifications say to reject it; accepting it
   would be a downgrade.
4. **The decryption keys are the key ring's asymmetric keys.** They are the `AsymmetricSecretKey`s of
   `registration.KeyRing`
   ([ADR-0270](0270-the-openid-protocol-client-uses-the-familys-jose-stack-and-named-key-rings.md)), passed to the JWT
   validator with `UseDecryptionKeys`, which selects them by `use` and `alg`. A client that registered encryption but
   has no key ring throws `InvalidOperationException`.
5. **A replicated `iss` header must agree with the issuer.** When the JWE header replicates `iss` (OpenID Connect
   Back-Channel Logout 1.0 §2.4, RFC 7519 §5.3), it must be a string equal to the issuer, else the token fails with
   `invalid_issuer`. This applies to both kinds of token.
6. **A decryption failure has its own code.** `TokenValidationFailureCodes.DecryptionFailed` is an alias of
   `JwtValidationFailureCodes.DecryptionFailed` (`decryption_failed`,
   [ADR-0278](0278-token-validation-failures-carry-a-machine-readable-code.md)). The JWT validator resolves the
   signing keys only for the enclosed JWS, which exists only after decryption. A `JwtDecodeException` or
   `JwtSecretKeyNotFoundException` raised before the validator's `ResolveValidationKeysAsync` delegate ran therefore
   came from decryption and maps to `decryption_failed` with a fixed text; after the delegate ran it maps to
   `invalid_signature`, as before. The text never contains the token.
7. **The shared logic lives in one place.** The internal `ClientDecryptionPolicy`, beside `ProviderSignaturePolicy`,
   holds the permitted algorithms, the decryption keys and the replicated-issuer check. `DefaultIdTokenValidator` and
   `DefaultLogoutTokenValidator` each take an `IClientKeyRingProvider`, which `AddOpenIdClientLibrary` already
   registers.
8. **The decryption checks run before the claim checks.** The algorithm policy, the decryption and the replicated `iss`
   check come first, then the claim checks of ADR-0273 and ADR-0274. The logout replay check stays last, so a token
   that fails any other check never records its `jti`.

## Options considered

- **Single-valued properties** (`IdTokenKeyManagementAlgorithm`, `IdTokenContentEncryptionAlgorithm`). The lists match
  `IdTokenSigningAlgorithms` and the OP's own settings. Rejected.
- **A null list permits every algorithm.** The conventions use explicit allow-lists
  ([ADR-0154](0154-jwts-are-validated-as-signed-tokens-under-explicit-algorithm-and-key-policy.md)); an empty list
  permits nothing. Rejected.
- **Shared-key encryption** (`dir`, AES key wrap, PBES2). The OP encrypts only to a public key the client published,
  and a `private_key_jwt` client has no shared secret. Rejected.
- **Accept an unencrypted token when encryption is registered.** It is a downgrade that lets an attacker who cannot
  encrypt to the client replace the encrypted token with a plain one. Rejected.
- **Parse the token first to tell a decryption failure.** It duplicates the JWT validator's parse, and the validator
  already shows, by whether it asked for signing keys, whether decryption succeeded. Rejected.
- **Consult the provider's encryption metadata.** The registration fixes the values, so the provider's list adds
  nothing the client can trust more. Rejected.

## Consequences

- A client that registers encryption sets both `IdTokenKeyManagementAlgorithms` and `IdTokenContentEncryptionAlgorithms`
  and keeps its private decryption key, with `use` `enc`, in its key ring.
- Tokens from an OP that does not encrypt are refused for such a client.
- A client that registers no encryption keeps refusing an encrypted token, now with `algorithm_not_permitted`.
- This amends [ADR-0273](0273-the-protocol-client-validates-id-tokens-under-an-asymmetric-algorithm-policy.md) point 8,
  [ADR-0274](0274-the-protocol-client-validates-logout-tokens-and-rejects-replays.md) point 8 and
  [ADR-0278](0278-token-validation-failures-carry-a-machine-readable-code.md) points 7 and 8, through their status lines
  only.
- An end-to-end test against the in-process OP remains.

## References

- `src/NCode.Identity.OpenId.Client/Tokens/ClientDecryptionPolicy.cs`
- OpenID Connect Core 1.0 §3.1.3.7 step 1; OpenID Connect Back-Channel Logout 1.0 §2.4 and §2.6 step 1; OpenID Connect
  Dynamic Client Registration 1.0 §2
- RFC 7519 §5.3 (replicating claims as header parameters); RFC 8725 §3.2 and §3.6 (`RSA1_5`; compression)
- [ADR-0123](0123-numeric-date-is-read-strictly-and-range-guarded.md) — the strict NumericDate read that the same two
  validators use for `auth_time` and the logout `exp`
- [ADR-0154](0154-jwts-are-validated-as-signed-tokens-under-explicit-algorithm-and-key-policy.md) — JWTs are validated
  only through `IJwtValidator` under explicit algorithm and key policy
- [ADR-0270](0270-the-openid-protocol-client-uses-the-familys-jose-stack-and-named-key-rings.md) — the client's named
  key rings
- [ADR-0273](0273-the-protocol-client-validates-id-tokens-under-an-asymmetric-algorithm-policy.md) — the ID-token
  validator this amends
- [ADR-0274](0274-the-protocol-client-validates-logout-tokens-and-rejects-replays.md) — the logout-token validator this
  amends
- [ADR-0278](0278-token-validation-failures-carry-a-machine-readable-code.md) — the failure codes this extends
- Issues [#347](https://github.com/NCodeGroup/Identity/issues/347) and
  [#342](https://github.com/NCodeGroup/Identity/issues/342)
