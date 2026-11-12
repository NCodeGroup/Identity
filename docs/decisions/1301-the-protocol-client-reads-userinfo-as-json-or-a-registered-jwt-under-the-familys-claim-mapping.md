# 1301. The protocol client reads UserInfo as JSON or a registered JWT under the family's claim mapping

- **Status:** Accepted
- **Date:** 2026-10-29
- **Deciders:** NCode Group

## Context

Some providers keep profile claims (`email`, `name`, groups) out of the ID token and serve them only from the UserInfo
endpoint (OpenID Connect Core 1.0 §5.3). The protocol client
([ADR-1285](1285-the-protocol-client-is-a-framework-free-library-over-the-familys-jose-stack-and-named-key-rings.md))
reads it for server-side callers, and the BFF builds a session's claims from the ID token
([ADR-1290](1290-the-bff-keeps-each-session-as-an-encrypted-server-side-record-under-a-cookie-handle.md)) and, when
asked, from UserInfo too.

A UserInfo request presents an access token, so it is sent the way that token was issued: as a bearer token, or with a
DPoP proof of the key it is bound to (RFC 9449 §7), and over mutual TLS to the endpoint's alias when the client
authenticates with it, so a certificate-bound token is accepted (RFC 8705 §5). OIDC Core §5.3.2 requires the client to
check that the response's `sub` is exactly the ID token's, because a response about another user must not be used.

The response is JSON, unless the client registered `userinfo_signed_response_alg` or `userinfo_encrypted_response_alg`
(OpenID Connect Dynamic Client Registration 1.0 §2): then it is an `application/jwt` that carries `iss` and an `aud`
naming the client when signed, and is a Nested JWT when both signed and encrypted.

The protocol client already validates the provider's other JWTs the same way, the ID token
([ADR-1273](1273-the-protocol-client-validates-id-tokens-and-their-token-hashes-under-openid-connect-core.md)) and the
logout token
([ADR-1274](1274-the-protocol-client-validates-logout-tokens-and-records-each-only-after-the-client-handles-it.md)),
through one core
([ADR-1280](1280-the-protocol-client-validates-and-decrypts-every-provider-issued-jwt-through-one-core-under-registered-algorithms.md)),
and the family maps JSON claims to `Claim`s once, in the JWT stack, typing each claim by its JSON value
([ADR-1122](1122-jwt-claim-values-map-to-and-from-json-by-their-declared-type-alone.md)). A UserInfo reader that copied
either would drift from it.

## Decision

**A client that registered UserInfo signing asks for, and accepts only, a JWT that it validates as it validates an ID
token, under the algorithms it registered; a client that registered no UserInfo signing accepts only JSON. Either
way the claims are mapped through the family's one claim mapping.**

1. **`IUserInfoClient`.** `GetUserInfoAsync(registration, UserInfoRequest)` sends a `GET` to `userinfo_endpoint`, or to
   its mTLS alias when the authenticator uses mutual TLS. A token whose `UserInfoRequest.DPoPJwkThumbprint` is set is
   sent as `Authorization: DPoP` with a proof of that key, and a `use_dpop_nonce` challenge is retried exactly once with
   the nonce (RFC 9449 §9); any other token is sent as a bearer token.
2. **The registration declares the format.** `ClientRegistration.UserInfoSigningAlgorithms`,
   `UserInfoKeyManagementAlgorithms` and `UserInfoContentEncryptionAlgorithms` mirror the client's
   `userinfo_signed_response_alg`, `userinfo_encrypted_response_alg` and `userinfo_encrypted_response_enc`, and are
   validated as the ID-token lists are. An empty `UserInfoSigningAlgorithms` means the client registered no signing, so
   the response is JSON, and the request's `Accept` header asks for `application/jwt` or `application/json` to match.
3. **A response in the other format is refused.** A JSON response to a client that registered signing is a downgrade to
   claims nobody signed, and a JWT to a client that registered none has no algorithm the client chose. Both are
   `unsupported_response`.
4. **A response that is encrypted but not signed is never accepted.** OIDC Core allows one, but the family accepts only
   signed content, because anyone holding the client's public key can produce a JWE. A registration with UserInfo
   encryption and no UserInfo signing, or with content encryption algorithms and no key management algorithm, is a
   configuration error: `IUserInfoClient` throws `InvalidOperationException` before it sends a request.
5. **A signed or Nested UserInfo JWT is validated through the shared core.** The signature algorithm must be
   registered for UserInfo, asymmetric and, when the provider lists
   `OpenIdProviderMetadata.UserInfoSigningAlgValuesSupported`, advertised. `iss` must be the provider's issuer, `aud`
   must name the client, and `typ`, when present, must be `JWT`. `exp`, `nbf` and `iat` are honored when present but
   not required, because §5.3.2 requires none of them. A failed check reports its `TokenValidationFailureCodes` value.
6. **One claim mapping.** `IClaimsIdentityFactory` (`NCode.Identity.Jwt.Abstractions`) maps any JSON claim set to a
   `ClaimsIdentity`. A JSON object, or a validated JWT payload, whose `sub` equals `UserInfoRequest.ExpectedSubject`
   is a `UserInfoResult` with the claims and an `Identity` mapped through it, so a UserInfo claim and an ID-token claim
   of the same JSON value are the same `Claim`.
7. **Failures.** A `WWW-Authenticate` error (RFC 6750 §3), or an OAuth error body, is a `ProtocolError`. Any other
   failure is a `UserInfoFailureCodes` value (`subject_mismatch`, `invalid_issuer`, `invalid_response`,
   `unsupported_response`), or a `TokenValidationFailureCodes` value for a JWT. A response with neither claims nor an
   error throws `OpenIdProviderException`, and a provider with no UserInfo endpoint throws `InvalidOperationException`.
8. **The BFF names what it registered.** `Bff:Frontends:{name}:IdToken` (`BffIdTokenOptions`) and `:UserInfo`
   (`BffUserInfoOptions`) each have `SigningAlgorithms`, `KeyManagementAlgorithms` and `ContentEncryptionAlgorithms`
   that map onto the registration, and `UserInfo` also has `Load` (default `false`). The host refuses to start on a
   blank entry, `none` or a duplicate, on content encryption without key management, on UserInfo encryption without
   UserInfo signing, and on encryption without `ClientAuthentication:KeyRing`, which holds the decryption keys.
9. **The BFF merges UserInfo once, at login, when asked.** With `UserInfo:Load`, the callback requests UserInfo after
   the ID token validates and before the claim contributors and the login gates. Each UserInfo claim type replaces the
   ID token's claims of that type, because UserInfo is the provider's current profile; `iss`, `sub`, `sid`,
   `auth_time`, `acr`, `amr`, which anchor the session to the authentication, and the validation-only claims are never
   taken from it. A UserInfo error ends the login with `userinfo_failed` and an unreachable provider with
   `provider_unavailable` (`BffLoginFailureReasons`). UserInfo is not requested again at refresh; a session keeps the
   claims it was created with.

## Options considered

- **Map UserInfo JSON separately in the client or the BFF.** Rejected: a second mapping drifts from the JWT stack's.
- **Add UserInfo claims only when the ID token lacks the type.** Rejected: the ID token can carry a stale value, and
  two values of `email` from two sources leave the app to guess which to trust.
- **Request UserInfo at every refresh.** Rejected: it costs a request per refresh, and a session's claims would change
  under the app between requests; the session claim contributors are the place for data that must be fresh.
- **Accept an unverified signed response.** Rejected: an unverified JWT is no better than an unauthenticated document.
- **POST to the endpoint.** Rejected: OIDC Core allows both, `GET` is the common form, and the token travels in the
  `Authorization` header either way.
- **Validate a JWT whenever the provider sends one, under the ID-token algorithms.** Rejected: UserInfo signing is its
  own registration, and the ID-token algorithms say nothing about what the client agreed to for UserInfo.
- **Accept JSON from a provider when the client registered signing.** Rejected: it lets an attacker who can alter the
  response strip the signature the client asked for.
- **Decrypt a response that is encrypted but not signed.** Rejected: it needs a second path around the JWT validator's
  signed-only rule, and the client gets the same protection by registering signing as well.
- **Copy the ID-token validation into the UserInfo client.** Rejected: three copies of the same security checks drift.

The chosen design reads UserInfo in the format the client registered, validates a JWT through the shared core, and maps
claims once.

## Consequences

- A BFF in front of a provider that keeps profile claims out of the ID token gets them with one setting.
- Any server-side client of the family reads UserInfo with the same proof-of-possession and mTLS handling as its token
  requests.
- A provider that signs, or signs and encrypts, UserInfo works when the client registers the same algorithms.
- A provider configured to encrypt UserInfo without signing it cannot be used; the client registers signing too.
- A JSON-only client cannot be tricked into trusting a signed document it did not ask for, and a signing client cannot
  be downgraded to JSON.
- Tests cover the request forms (bearer, DPoP with its nonce retry, mTLS), each refused format, the failure codes, the
  configuration errors and the BFF's merge rules.

## References

- [OpenID Connect Core 1.0 §5.3](https://openid.net/specs/openid-connect-core-1_0.html#UserInfo), §5.3.2 — the
  UserInfo endpoint and response
- [OpenID Connect Dynamic Client Registration 1.0 §2](https://openid.net/specs/openid-connect-registration-1_0.html#ClientMetadata)
  — `userinfo_signed_response_alg`, `userinfo_encrypted_response_alg`, `userinfo_encrypted_response_enc`
- [OpenID Connect Discovery 1.0 §3](https://openid.net/specs/openid-connect-discovery-1_0.html#ProviderMetadata) —
  `userinfo_signing_alg_values_supported`
- [RFC 6750 §3](https://www.rfc-editor.org/rfc/rfc6750#section-3) — the `WWW-Authenticate` error
- [RFC 9449 §7, §9](https://www.rfc-editor.org/rfc/rfc9449#section-7) — DPoP-bound access tokens and resource nonces
- [RFC 8705 §5](https://www.rfc-editor.org/rfc/rfc8705#section-5) — mTLS endpoint aliases
- [RFC 8725 §3.1, §3.2](https://www.rfc-editor.org/rfc/rfc8725#section-3.1) — algorithm verification and selection
- [ADR-1122](1122-jwt-claim-values-map-to-and-from-json-by-their-declared-type-alone.md) — claim mapping
- [ADR-1280](1280-the-protocol-client-validates-and-decrypts-every-provider-issued-jwt-through-one-core-under-registered-algorithms.md)
  — the shared validation core
- [ADR-1290](1290-the-bff-keeps-each-session-as-an-encrypted-server-side-record-under-a-cookie-handle.md) — BFF sessions
- Code: `IUserInfoClient`, `DefaultUserInfoClient`, `IClaimsIdentityFactory`, `BffCallbackHandler`
- Issue [#421](https://github.com/NCodeGroup/identity/issues/421)
