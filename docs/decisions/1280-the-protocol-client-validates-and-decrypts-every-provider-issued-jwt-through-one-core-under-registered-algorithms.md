# 1280. The protocol client validates and decrypts every provider-issued JWT through one core under registered algorithms

- **Status:** Accepted
- **Date:** 2026-10-28
- **Deciders:** NCode Group

## Context

The protocol client receives three kinds of JWT from the provider: the ID token
([ADR-1273](1273-the-protocol-client-validates-id-tokens-and-their-token-hashes-under-openid-connect-core.md)), the
logout token
([ADR-1274](1274-the-protocol-client-validates-logout-tokens-and-records-each-only-after-the-client-handles-it.md)) and
a signed UserInfo response
([ADR-1301](1301-the-protocol-client-reads-userinfo-as-json-or-a-registered-jwt-under-the-familys-claim-mapping.md)).
Each kind checks its own claims, but the work around the claims is the same: choose the permitted algorithms, resolve
the provider's keys, decrypt, check a replicated issuer, verify the signature and word the failure. That work must exist
once, so a fix to it reaches every kind.

The NCode OP encrypts both the ID token and the logout token to a client that registers
`id_token_encrypted_response_alg`. A protocol client that registered encryption therefore cannot validate any token the
OP sends it unless it decrypts. OpenID Connect Core §3.1.3.7 step 1 requires the client to decrypt an ID token with the
key and algorithms it registered, and OpenID Connect Back-Channel Logout 1.0 §2.6 step 1 requires the same for a logout
token (issue [#347](https://github.com/NCodeGroup/identity/issues/347)).

The conventions require a received JWT to be validated only through `IJwtValidator` under an explicit allow-list of
algorithms and keys
([ADR-1154](1154-jwts-are-validated-only-as-signed-tokens-by-requirements-and-handlers-under-the-callers-stated-policy.md)).
The decryption keys are the client's own private keys, which the client holds in a named key ring
([ADR-1285](1285-the-protocol-client-is-a-framework-free-library-over-the-familys-jose-stack-and-named-key-rings.md)).

## Decision

**The protocol client validates every JWT the provider issues through one core, under the signature, key management and
content encryption algorithms the client registered, and decrypts with the asymmetric keys of the client's key ring. When
the client registered encryption, every token must be encrypted.**

1. **One validator, one profile per kind.** The internal `ProviderJwtValidator` validates every provider-issued JWT
   against an internal `ProviderJwtProfile`. The profile (`ForIdToken`, `ForLogoutToken`, `ForUserInfo`) names the
   registered and advertised algorithms, the `typ`, the lifetime rules and the words of the failure reasons. Each kind
   ([ADR-1273](1273-the-protocol-client-validates-id-tokens-and-their-token-hashes-under-openid-connect-core.md),
   [ADR-1274](1274-the-protocol-client-validates-logout-tokens-and-records-each-only-after-the-client-handles-it.md),
   [ADR-1301](1301-the-protocol-client-reads-userinfo-as-json-or-a-registered-jwt-under-the-familys-claim-mapping.md))
   checks only its own claims.
2. **The signature policy is an allow-list.** The internal static `ProviderSignaturePolicy` permits the registered
   `SignatureAlgorithm`s whose key type is `AsymmetricSecretKey`, so `none` is never permitted. It intersects them with
   the registration's list when that list is non-empty and with the provider's advertised list when the provider
   publishes one, and fails on an empty result. It passes the result as `ValidSignatureAlgorithms`, so the header is
   checked before any cryptography runs.
3. **The provider's keys come from `IProviderKeySetResolver`.** An unknown `kid` triggers one rate-limited refresh.
   Without a `kid`, every key is a candidate, filtered by `use` and `alg`.
4. **The registration lists the encryption algorithms.** `ClientRegistration.IdTokenKeyManagementAlgorithms`
   (`id_token_encrypted_response_alg`) and `IdTokenContentEncryptionAlgorithms` (`id_token_encrypted_response_enc`) are
   lists, like `IdTokenSigningAlgorithms`. A non-empty key management list means the client registered encryption. An
   empty content encryption list means `A128CBC-HS256`, the registration default (OpenID Connect Dynamic Client
   Registration §2, as the NCode OP does). A content encryption list without a key management list throws
   `InvalidOperationException`, a configuration error
   ([ADR-1285](1285-the-protocol-client-is-a-framework-free-library-over-the-familys-jose-stack-and-named-key-rings.md)). The UserInfo
   counterparts follow the same rules.
5. **Only asymmetric key management is permitted.** The internal `ClientDecryptionPolicy` permits the registered key
   management algorithms whose key type is `AsymmetricSecretKey` (RSA-OAEP\*, ECDH-ES\*): never `RSA1_5` (RFC 8725
   §3.2), never `dir`, AES key wrap or PBES2, because the OP encrypts only to a public key the client published. It
   permits only the registered `AuthenticatedEncryptionAlgorithm`s and no compression
   (`ValidCompressionAlgorithms = []`, RFC 8725 §3.6). It never consults the provider's
   `*_encryption_*_values_supported` metadata: the registration fixes the values.
6. **Encryption is all or nothing.** When the client registered no encryption, a JWE fails with
   `algorithm_not_permitted`. When it registered encryption, a plain JWS fails with `invalid_token`, because accepting
   one is a downgrade.
7. **The decryption keys are the key ring's asymmetric keys.** They are the `AsymmetricSecretKey`s of
   `registration.KeyRing`
   ([ADR-1285](1285-the-protocol-client-is-a-framework-free-library-over-the-familys-jose-stack-and-named-key-rings.md)), passed to the JWT
   validator with `UseDecryptionKeys`. A decryption key carries `use` `enc`. A client that registered encryption but has
   no key ring throws `InvalidOperationException`.
8. **A replicated `iss` header must agree with the issuer.** When the JWE header replicates `iss` (OpenID Connect
   Back-Channel Logout 1.0 §2.4, RFC 7519 §5.3), it must be a string equal to the issuer, or the token fails with
   `invalid_issuer`.
9. **A decryption failure has its own code, and failure text never carries the token.** The JWT validator resolves the
   signing keys only for the enclosed JWS, which exists only after decryption. A `JwtDecodeException` or
   `JwtSecretKeyNotFoundException` raised before `ResolveValidationKeysAsync` ran therefore came from decryption and
   maps to `decryption_failed`
   ([ADR-1278](1278-token-validation-failures-carry-a-machine-readable-code-set-where-the-failure-is-detected.md));
   raised after it, it maps to `invalid_signature`. Each carries fixed text: `decryption_failed` reads "The {token}
   could not be decrypted with the client's keys." and `invalid_signature` reads "The {token} signature could not be
   verified with the provider's signing keys.". Any other failure reads "The {token} is invalid: " followed by its
   message.
10. **Provider failures and cancellation.** A provider failure throws `OpenIdProviderException`, rethrown from the JWT
    validation result. Cancellation propagates.
11. **The order of checks is fixed.** The algorithm policy comes first. The JWT validator then decrypts, verifies the
    signature and checks the issuer, audience and lifetime. The core then checks that a client that registered
    encryption received a JWE, the replicated `iss` and the `typ`, and then the kind runs its claim checks. The logout replay lookup runs last, so a token that fails any other check
    never records its `jti`.

## Options considered

- **Copy the validation per token kind.** Each kind would carry its own algorithm policy, decryption and failure text,
  and a fix would have to reach each copy. Rejected.
- **Single-valued properties** (`IdTokenKeyManagementAlgorithm`, `IdTokenContentEncryptionAlgorithm`). The lists match
  `IdTokenSigningAlgorithms` and the OP's own settings. Rejected.
- **A null list that permits everything.** The conventions use explicit allow-lists
  ([ADR-1154](1154-jwts-are-validated-only-as-signed-tokens-by-requirements-and-handlers-under-the-callers-stated-policy.md));
  an empty list permits nothing. Rejected.
- **Shared-key encryption** (`dir`, AES key wrap, PBES2). The OP encrypts only to a public key the client published, and
  a `private_key_jwt` client has no shared secret. Rejected.
- **Accept an unencrypted token when encryption is registered.** It is a downgrade that lets an attacker who cannot
  encrypt to the client replace the encrypted token with a plain one. Rejected.
- **Parse the token first to tell a decryption failure.** It duplicates the JWT validator's parse, and the validator
  already shows, by whether it asked for signing keys, whether decryption succeeded. Rejected.
- **Consult the provider's encryption metadata.** The registration fixes the values, so the provider's list adds nothing
  the client can trust more. Rejected.
- **One core under registered algorithms, decrypting with the key ring.** The core owns the policy, the decryption and
  the failure text once, and each kind keeps only its claims. Chosen.

## Consequences

- A client that registers encryption sets both `IdTokenKeyManagementAlgorithms` and `IdTokenContentEncryptionAlgorithms`
  and keeps its private decryption key, with `use` `enc`, in its key ring.
- Tokens from an OP that does not encrypt are refused for such a client.
- A client that registers no encryption refuses an encrypted token with `algorithm_not_permitted`.
- A fix to the algorithm policy, the decryption or the failure text reaches the ID token, the logout token and the
  UserInfo response at once.

## References

- `src/NCode.Identity.OpenId.Client/Tokens/ProviderJwtValidator.cs`, `ProviderJwtProfile.cs`,
  `ProviderSignaturePolicy.cs` and `ClientDecryptionPolicy.cs`
- OpenID Connect Core 1.0 §3.1.3.7 step 1; OpenID Connect Back-Channel Logout 1.0 §2.4 and §2.6 step 1; OpenID Connect
  Dynamic Client Registration 1.0 §2
- RFC 7519 §5.3 (replicating claims as header parameters); RFC 8725 §3.2 and §3.6 (`RSA1_5`; compression)
- [ADR-1123](1123-a-numericdate-and-a-whole-number-a-remote-party-sends-are-read-strictly-through-shared-readers.md) —
  the strict NumericDate read that the validators use for `auth_time` and the logout `exp`
- [ADR-1154](1154-jwts-are-validated-only-as-signed-tokens-by-requirements-and-handlers-under-the-callers-stated-policy.md) —
  JWTs are validated only through `IJwtValidator` under explicit algorithm and key policy
- [ADR-1285](1285-the-protocol-client-is-a-framework-free-library-over-the-familys-jose-stack-and-named-key-rings.md) — the client's named
  key rings
- [ADR-1273](1273-the-protocol-client-validates-id-tokens-and-their-token-hashes-under-openid-connect-core.md) — the
  ID-token claim checks
- [ADR-1274](1274-the-protocol-client-validates-logout-tokens-and-records-each-only-after-the-client-handles-it.md) — the
  logout-token claim checks
- [ADR-1278](1278-token-validation-failures-carry-a-machine-readable-code-set-where-the-failure-is-detected.md) — the
  failure codes
- [ADR-1301](1301-the-protocol-client-reads-userinfo-as-json-or-a-registered-jwt-under-the-familys-claim-mapping.md) —
  the UserInfo claim checks
- Issue [#347](https://github.com/NCodeGroup/identity/issues/347)
