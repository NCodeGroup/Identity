# 301. A signed UserInfo response is validated like an ID token under the client's registered algorithms

- **Status:** Accepted
- **Date:** 2026-10-29
- **Deciders:** NCode Group

## Context

OpenID Connect Core 1.0 §5.3.2 returns UserInfo as JSON, unless the client registered `userinfo_signed_response_alg`
or `userinfo_encrypted_response_alg` (OpenID Connect Dynamic Client Registration 1.0 §2): then the response is an
`application/jwt` that carries `iss` and an `aud` naming the client when signed, and is a Nested JWT when both signed
and encrypted. [ADR-0299](0299-userinfo-is-read-as-json-under-the-familys-claim-mapping-and-merged-once-at-login.md)
shipped `IUserInfoClient` for JSON only and refused a JWT as `unsupported_response`, so a provider that signs UserInfo
could not be used with the BFF's UserInfo option (issue #421).

The protocol client already validates two kinds of JWT from the provider the same way: the ID token
([ADR-0273](0273-the-protocol-client-validates-id-tokens-under-an-asymmetric-algorithm-policy.md)) and the logout token
([ADR-0274](0274-the-protocol-client-validates-logout-tokens-and-rejects-replays.md)), each decrypted with the client's key ring
when the client registered encryption
([ADR-0280](0280-the-protocol-client-decrypts-id-and-logout-tokens-with-its-key-ring.md)). The two validators were
copies of each other, and a third copy for UserInfo would have drifted.

## Decision

**A client that registered UserInfo signing asks for, and accepts only, a JWT that it validates as it validates an ID
token, under the algorithms it registered; a client that registered no UserInfo signing accepts only JSON.**

1. **The registration declares the format.** `ClientRegistration` gains `UserInfoSigningAlgorithms`,
   `UserInfoKeyManagementAlgorithms` and `UserInfoContentEncryptionAlgorithms`, the client's
   `userinfo_signed_response_alg`, `userinfo_encrypted_response_alg` and `userinfo_encrypted_response_enc`, validated
   as the ID-token members are. Unlike `IdTokenSigningAlgorithms`, where an empty list accepts any asymmetric algorithm
   because an ID token is always signed, an empty `UserInfoSigningAlgorithms` means the client registered no signing,
   so the response is JSON. The request's `Accept` header asks for `application/jwt` or `application/json` to match.
2. **A response in the other format is refused.** A JSON response to a client that registered signing would downgrade
   it to claims nobody signed, and a JWT to a client that registered none has no algorithm the client chose. Both are
   `unsupported_response`.
3. **A response that is encrypted but not signed is never accepted.** OIDC Core allows one, but the family's JWT
   validation accepts only signed content, because anyone holding the client's public key can produce a JWE. A
   registration with UserInfo encryption and no UserInfo signing, or content encryption algorithms with no key
   management algorithm, is a configuration error: `IUserInfoClient` throws `InvalidOperationException` before it sends
   a request.
4. **The checks are the ID token's.** The signature algorithm must be registered for UserInfo, asymmetric (never
   `none` or a symmetric algorithm) and, when the provider advertises `userinfo_signing_alg_values_supported` (now
   `OpenIdProviderMetadata.UserInfoSigningAlgValuesSupported`), one it advertises. The provider's keys verify it,
   selected by `kid` with one refresh. A Nested JWT decrypts with the asymmetric keys of the client's key ring under its
   registered `alg` (RSA-OAEP or ECDH-ES, never `RSA1_5`) and `enc`, and an `iss` it replicates in its header must
   match. `iss` must equal the provider's issuer and `aud` must name the client (OIDC Core §5.3.2). A `typ` header, when
   present, must be `JWT`. `exp`, `nbf` and `iat` are honored when present but not required, because §5.3.2 requires
   none of them. Then the claims must carry the ID token's `sub`, and they are mapped through `IClaimsIdentityFactory`
   as JSON claims are. A failed check reports its `TokenValidationFailureCodes` value.
5. **One validation core for every JWT the provider issues to the client.** An internal `ProviderJwtValidator`
   validates a token against a `ProviderJwtProfile`, which names its registered and advertised algorithms, its `typ`,
   its lifetime rules and the words its failure reasons use. The ID-token, logout-token and UserInfo validations each
   take their profile from it and check only the claims particular to their kind: `nonce`, `azp`, `auth_time` and the
   token hashes for an ID token, `events`, `sid` and `jti` for a logout token, `sub` for UserInfo.
6. **The BFF names what it registered.** `Bff:IdToken` (`SigningAlgorithms`, `KeyManagementAlgorithms`,
   `ContentEncryptionAlgorithms`) and `Bff:UserInfo` (`Load` and the same three lists) map onto the registration;
   `Bff:UserInfo:Load` replaces `Bff:LoadUserInfo`. The host refuses to start on a blank entry, `none` or a duplicate, on
   content encryption without key management, on UserInfo encryption without UserInfo signing, and on encryption without
   `Bff:ClientAuthentication:KeyRing`, which holds the decryption keys.

## Options considered

- **Validate a JWT whenever the provider sends one, under the ID-token algorithms.** Rejected: UserInfo signing is its
  own registration, and the ID-token algorithms say nothing about what the client agreed to for UserInfo.
- **Accept JSON from a provider when the client registered signing.** Rejected: it lets an attacker who can alter the
  response strip the signature the client asked for.
- **Decrypt a UserInfo response that is encrypted but not signed.** Rejected: it needs a second path around the JWT
  validator's signed-only rule, and the client gets the same protection by registering signing as well.
- **Copy the ID-token validation into the UserInfo client.** Rejected: three copies of the same security checks drift.

## Consequences

- A provider that signs, or signs and encrypts, UserInfo works with the protocol client and the BFF when the client
  registers the same algorithms.
- A provider configured to encrypt UserInfo without signing it cannot be used; the client registers signing too.
- The BFF can register its ID-token signing and encryption algorithms, which it could not before.
- `ClientRegistration`, `OpenIdProviderMetadata` and `OpenIdProtocol.MetadataNames` gain public members, and
  `BffOptions.LoadUserInfo` becomes `BffOptions.UserInfo.Load`, a breaking change accepted under the pre-release
  posture ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)).

## References

- [OpenID Connect Core 1.0 §5.3.2](https://openid.net/specs/openid-connect-core-1_0.html#UserInfoResponse) — the
  UserInfo response
- [OpenID Connect Dynamic Client Registration 1.0 §2](https://openid.net/specs/openid-connect-registration-1_0.html#ClientMetadata)
  — `userinfo_signed_response_alg`, `userinfo_encrypted_response_alg`, `userinfo_encrypted_response_enc`
- [OpenID Connect Discovery 1.0 §3](https://openid.net/specs/openid-connect-discovery-1_0.html#ProviderMetadata) —
  `userinfo_signing_alg_values_supported`
- [RFC 8725 §3.1, §3.2](https://www.rfc-editor.org/rfc/rfc8725#section-3.1) — algorithm verification and selection
- [ADR-0299](0299-userinfo-is-read-as-json-under-the-familys-claim-mapping-and-merged-once-at-login.md) — UserInfo
- Issue [#421](https://github.com/NCodeGroup/Identity/issues/421)
